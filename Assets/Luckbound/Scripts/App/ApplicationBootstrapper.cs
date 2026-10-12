using UnityEngine;
using UnityEngine.SceneManagement;
using Luckbound.Core.Expeditions;

namespace Luckbound.App
{
    /// <summary>
    /// Owns the application lifetime and routes a fresh launch to the main menu.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ApplicationBootstrapper : MonoBehaviour
    {
        private static ApplicationBootstrapper _instance;
        private readonly ExpeditionSettlementLedger _settlementLedger = new ExpeditionSettlementLedger();

        public static ApplicationBootstrapper Instance => _instance;

        public ExpeditionRunConfiguration ActiveRun { get; private set; }
        public ExpeditionSettlementLedger SettlementLedger => _settlementLedger;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "Bootstrap")
            {
                SceneManager.LoadScene("Menu", LoadSceneMode.Single);
            }
        }

        public void BeginExpedition(ExpeditionRunConfiguration run)
        {
            ActiveRun = run ?? throw new System.ArgumentNullException(nameof(run));
        }

        public void ClearActiveRun()
        {
            ActiveRun = null;
        }

        public ExpeditionSettlementReceipt SettleActiveRun(ExpeditionOutcome outcome, int accumulatedRenown)
        {
            if (ActiveRun == null) throw new System.InvalidOperationException("There is no active expedition to settle.");
            return _settlementLedger.Settle(ActiveRun.RunId, outcome, accumulatedRenown,
                ActiveRun.Inventory, ActiveRun.StartingEquipmentInstanceIds);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
