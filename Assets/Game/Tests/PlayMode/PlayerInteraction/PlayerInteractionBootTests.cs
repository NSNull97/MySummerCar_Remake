using System.Collections;
using MSC.Interaction.Carrying;
using MSC.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class PlayerInteractionBootTests
    {
        private const string PrototypeScenePath =
            "Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity";

        [UnityTest]
        public IEnumerator PrototypeSceneBootsConfiguredPlayerAndStablePickupTargets()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                PrototypeScenePath,
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            Scene scene = SceneManager.GetSceneByPath(PrototypeScenePath);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            PlayerInteractionPrototypeMarker marker = FindInScene<PlayerInteractionPrototypeMarker>(scene);
            PlayerInputRouter inputRouter = FindInScene<PlayerInputRouter>(scene);
            PlayerInteractionController interaction = FindInScene<PlayerInteractionController>(scene);
            CrossdotPresenter crossdot = FindInScene<CrossdotPresenter>(scene);
            PhysicsPickupTarget[] pickupTargets = FindAllInScene<PhysicsPickupTarget>(scene);

            Assert.That(marker, Is.Not.Null);
            Assert.That(inputRouter, Is.Not.Null);
            Assert.That(inputRouter.IsReady, Is.True);
            Assert.That(interaction, Is.Not.Null);
            Assert.That(crossdot, Is.Not.Null);
            Assert.That(crossdot.Visible, Is.True);
            Assert.That(pickupTargets.Length, Is.GreaterThanOrEqualTo(2));
            foreach (PhysicsPickupTarget target in pickupTargets)
            {
                Assert.That(target.StableId.IsValid, Is.True, target.name);
            }

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            T[] all = FindAllInScene<T>(scene);
            return all.Length > 0 ? all[0] : null;
        }

        private static T[] FindAllInScene<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] components = root.GetComponentsInChildren<T>(true);
                if (components.Length > 0)
                {
                    return components;
                }
            }

            return System.Array.Empty<T>();
        }
    }
}
