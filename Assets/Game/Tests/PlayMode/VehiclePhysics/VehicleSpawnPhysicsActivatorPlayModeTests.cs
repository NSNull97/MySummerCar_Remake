using System.Collections;
using System.Linq;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class VehicleSpawnPhysicsActivatorPlayModeTests
    {
        [UnityTest]
        public IEnumerator SleepingBodyWakesAndIgnoresPlayerOnlySupportProxy()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            int playerLayer = LayerMask.NameToLayer("Player");
            Assert.That(worldSurfaceLayer, Is.EqualTo(6));
            Assert.That(playerLayer, Is.EqualTo(9));

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "World surface";
            ground.layer = worldSurfaceLayer;
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.05f, 0f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(10f, 0.1f, 10f);

            var chassis = new GameObject("Sleeping chassis");
            chassis.transform.position = new Vector3(0f, 2f, 0f);
            Rigidbody body = chassis.AddComponent<Rigidbody>();
            body.mass = 389f;
            body.useGravity = true;
            body.isKinematic = false;
            BoxCollider structural = chassis.AddComponent<BoxCollider>();
            structural.size = Vector3.one * 0.5f;
            structural.excludeLayers = 1 << playerLayer;
            structural.layerOverridePriority = 1;

            var playerProxyObject = new GameObject("PlayerColl_test");
            playerProxyObject.transform.SetParent(chassis.transform, false);
            playerProxyObject.transform.localPosition =
                new Vector3(0f, -0.75f, 0f);
            Rigidbody playerProxyBody =
                playerProxyObject.AddComponent<Rigidbody>();
            playerProxyBody.isKinematic = true;
            playerProxyBody.useGravity = false;
            BoxCollider playerProxy =
                playerProxyObject.AddComponent<BoxCollider>();
            playerProxy.size = Vector3.one * 0.5f;
            playerProxy.excludeLayers = ~(1 << playerLayer);
            playerProxy.layerOverridePriority = 1;

            VehicleSpawnPhysicsActivator activator =
                chassis.AddComponent<VehicleSpawnPhysicsActivator>();
            activator.Configure(body, configuredActivationFixedSteps: 2);
            Physics.SyncTransforms();
            body.Sleep();

            try
            {
                Assert.That(body.IsSleeping(), Is.True);
                for (int step = 0; step < 80; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(
                    chassis.transform.position.y,
                    Is.InRange(0.2f, 0.34f),
                    "The structural box must settle on the road. If the " +
                    "PlayerColl proxy participates, the body stops near y=1.");
                Assert.That(activator.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(chassis);
                Object.Destroy(ground);
            }
        }

        [UnityTest]
        public IEnumerator GeneratedSatsuma_SeparatesPlayerFromDynamicChassis()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            Assert.That(playerLayer, Is.EqualTo(9));

            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = Object.Instantiate(
                prefab,
                new Vector3(0f, 100f, 0f),
                Quaternion.identity);
            yield return null;

            try
            {
                Rigidbody chassisBody = instance.GetComponent<Rigidbody>();
                Assert.That(chassisBody, Is.Not.Null);
                Assert.That(chassisBody.isKinematic, Is.False);

                Transform collisionRoot = instance.transform.Find(
                    "Sanitized Donor Chassis Colliders");
                Assert.That(collisionRoot, Is.Not.Null);
                Transform playerProxy = collisionRoot.Find(
                    "Project Player Collision Proxy");
                Assert.That(playerProxy, Is.Not.Null);
                Rigidbody playerProxyBody = playerProxy
                    .GetComponent<Rigidbody>();
                Assert.That(playerProxyBody, Is.Not.Null);
                Assert.That(playerProxyBody.isKinematic, Is.True);

                Collider[] colliders = collisionRoot
                    .GetComponentsInChildren<Collider>(true);
                Collider[] playerOnly = colliders.Where(collider =>
                    collider.name.StartsWith("PlayerColl_"))
                    .ToArray();
                Collider[] worldFacing = colliders.Where(collider =>
                    !collider.name.StartsWith("PlayerColl_"))
                    .ToArray();

                Assert.That(playerOnly, Has.Length.EqualTo(4));
                Assert.That(
                    playerOnly,
                    Is.All.Matches<Collider>(collider =>
                        collider.attachedRigidbody == playerProxyBody &&
                        collider.excludeLayers.value ==
                            ~(1 << playerLayer) &&
                        collider.layerOverridePriority >= 1));
                Assert.That(worldFacing, Has.Length.EqualTo(25));
                Collider[] enabledWorldFacing = worldFacing
                    .Where(collider => collider.enabled)
                    .ToArray();
                Assert.That(enabledWorldFacing, Has.Length.EqualTo(24));
                Assert.That(
                    enabledWorldFacing,
                    Is.All.Matches<Collider>(collider =>
                        collider.attachedRigidbody == chassisBody &&
                        !collider.isTrigger));
                Assert.That(
                    worldFacing,
                    Is.All.Matches<Collider>(collider =>
                        (collider.excludeLayers.value &
                            (1 << playerLayer)) != 0 &&
                        collider.layerOverridePriority >= 1));
            }
            finally
            {
                Object.Destroy(instance);
            }
        }
    }
}
