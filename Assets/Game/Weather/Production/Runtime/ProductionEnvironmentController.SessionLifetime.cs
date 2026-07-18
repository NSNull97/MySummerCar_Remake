using MSC.Core.Lifecycle;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Production
{
    public sealed partial class ProductionEnvironmentController :
        IGameSessionLifetime
    {
        public void EndGameSession()
        {
            if (activeOwner != this)
            {
                return;
            }

            simulationActive = false;
            CancelTopologyRevalidation();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            adapter?.Detach();
            globalWetnessBridge.Reset();
            activeOwner = null;
        }
    }
}
