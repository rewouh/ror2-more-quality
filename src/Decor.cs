using System.Collections.Generic;
using System.Linq;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace MoreQuality
{
    // Server-decided looks that every game has to show: an item icon floating over a thief, a ghost or a
    // mimic, a model scale (smaller mimics, bigger crowned bosses), and illusions only one player may see.
    // The server calls Decorate(), which applies it at home and sends it to every client. Move() does the
    // same for the position of our own moving objects (the ghost carriers).
    internal static class Decor
    {
        private const float IconSize = 0.9f;

        private sealed class Message : INetMessage
        {
            public NetworkInstanceId Target;
            public NetworkInstanceId OnlyFor = NetworkInstanceId.Invalid;
            public float Scale;
            public int[] Items = new int[0];

            public void Serialize(NetworkWriter writer)
            {
                writer.Write(Target);
                writer.Write(OnlyFor);
                writer.Write(Scale);
                writer.Write(Items.Length);
                foreach (var item in Items) writer.Write(item);
            }

            public void Deserialize(NetworkReader reader)
            {
                Target = reader.ReadNetworkId();
                OnlyFor = reader.ReadNetworkId();
                Scale = reader.ReadSingle();
                var count = reader.ReadInt32();
                Items = new int[Mathf.Clamp(count, 0, 32)];
                for (var i = 0; i < Items.Length; i++) Items[i] = reader.ReadInt32();
            }

            public void OnReceived()
            {
                if (NetworkServer.active) return;   // the host applied it already
                ApplyWhenSpawned(this, 60);
            }
        }

        private sealed class Position : INetMessage
        {
            public NetworkInstanceId Target;
            public Vector3 Where;

            public void Serialize(NetworkWriter writer) { writer.Write(Target); writer.Write(Where); }
            public void Deserialize(NetworkReader reader) { Target = reader.ReadNetworkId(); Where = reader.ReadVector3(); }

            public void OnReceived()
            {
                if (NetworkServer.active) return;
                var target = Util.FindNetworkObject(Target);
                var follower = target ? target.GetComponent<Follower>() : null;
                if (follower) follower.target = Where;
            }
        }

        public static void Register()
        {
            NetworkingAPI.RegisterMessageType<Message>();
            NetworkingAPI.RegisterMessageType<Position>();
        }

        // scale: model size multiplier (1 = unchanged); items: icons to float above it (none clears them);
        // onlyFor: when given, every other player's game hides the target.
        public static void Decorate(GameObject target, float scale, IEnumerable<ItemIndex> items, GameObject onlyFor = null)
        {
            if (!target || !NetworkServer.active) return;
            var message = new Message
            {
                Scale = scale,
                Items = (items ?? Enumerable.Empty<ItemIndex>()).Select(i => (int)i).Take(32).ToArray(),
                OnlyFor = onlyFor && onlyFor.GetComponent<NetworkIdentity>() ? onlyFor.GetComponent<NetworkIdentity>().netId : NetworkInstanceId.Invalid
            };
            Apply(target, message);
            var identity = target.GetComponent<NetworkIdentity>();
            if (!identity) return;
            message.Target = identity.netId;
            message.Send(NetworkDestination.Clients);
        }

        // Moves one of our networked objects on every game (smoothly, through its Follower).
        public static void Move(GameObject target, Vector3 position)
        {
            if (!target || !NetworkServer.active) return;
            target.transform.position = position;
            var follower = target.GetComponent<Follower>();
            if (follower) follower.target = position;
            var identity = target.GetComponent<NetworkIdentity>();
            if (identity && (!follower || Time.time >= follower.nextSend))
            {
                if (follower) follower.nextSend = Time.time + 0.05f;
                new Position { Target = identity.netId, Where = position }.Send(NetworkDestination.Clients);
            }
        }

        // On clients the object glides toward the last position the server sent.
        internal sealed class Follower : MonoBehaviour
        {
            public Vector3 target;
            public float nextSend;
            private bool started;

            private void Update()
            {
                if (NetworkServer.active) return;
                if (!started) { target = transform.position; started = true; return; }
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-15f * Time.deltaTime));
            }
        }

        // ---------------------------------------------------------------- on every game

        private sealed class Decorated : MonoBehaviour
        {
            public Vector3 baseScale;
            public Transform model;
            public GameObject icons;
        }

        // The object may not have reached this client yet: try again for a moment.
        private static void ApplyWhenSpawned(Message message, int triesLeft)
        {
            var target = Util.FindNetworkObject(message.Target);
            if (target) { Apply(target, message); return; }
            if (triesLeft > 0) RoR2Application.onNextUpdate += () => ApplyWhenSpawned(message, triesLeft - 1);
        }

        private static void Apply(GameObject target, Message message)
        {
            var decorated = target.GetComponent<Decorated>() ?? target.AddComponent<Decorated>();
            var body = target.GetComponent<CharacterBody>();
            if (!decorated.model)
            {
                var locator = body ? body.modelLocator : null;
                decorated.model = locator ? (locator.modelBaseTransform ? locator.modelBaseTransform : locator.modelTransform) : body ? null : target.transform;
                if (decorated.model) decorated.baseScale = decorated.model.localScale;
            }
            if (decorated.model && message.Scale > 0f) decorated.model.localScale = decorated.baseScale * message.Scale;

            if (message.OnlyFor != NetworkInstanceId.Invalid && !SeenHere(message.OnlyFor))
            {
                // Someone else's illusion: no model, no hurtboxes, nothing to see or shoot.
                var model = body && body.modelLocator ? body.modelLocator.modelTransform : null;
                if (model) model.gameObject.SetActive(false);
            }

            if (decorated.icons) Object.Destroy(decorated.icons);
            if (message.Items.Length == 0) return;
            decorated.icons = new GameObject("MoreQualityIcons");
            decorated.icons.transform.SetParent(target.transform, false);
            var height = body ? body.radius * 1.6f + 1f : 1.2f;
            decorated.icons.transform.localPosition = Vector3.up * height;
            for (var i = 0; i < message.Items.Length; i++)
            {
                var def = ItemCatalog.GetItemDef((ItemIndex)message.Items[i]);
                if (!def || !def.pickupIconSprite) continue;
                var icon = new GameObject("Icon");
                icon.transform.SetParent(decorated.icons.transform, false);
                icon.transform.localPosition = Vector3.right * (i - (message.Items.Length - 1) / 2f) * IconSize;
                icon.transform.localScale = Vector3.one * IconSize / Mathf.Max(0.01f, target.transform.lossyScale.x);
                icon.AddComponent<SpriteRenderer>().sprite = def.pickupIconSprite;
                icon.AddComponent<Billboard>();
            }
        }

        // True when one of this game's own players is the given body.
        private static bool SeenHere(NetworkInstanceId bodyId)
        {
            foreach (var user in LocalUserManager.readOnlyLocalUsersList)
            {
                var body = user.cachedBody;
                var identity = body ? body.GetComponent<NetworkIdentity>() : null;
                if (identity && identity.netId == bodyId) return true;
            }
            return false;
        }

        // Turns to face the camera.
        internal sealed class Billboard : MonoBehaviour
        {
            private void LateUpdate()
            {
                var camera = Camera.main;
                if (camera) transform.rotation = camera.transform.rotation;
            }
        }
    }
}
