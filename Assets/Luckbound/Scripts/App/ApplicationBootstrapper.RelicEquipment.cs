using System.Globalization;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        public Task<ExpeditionMutationCommitResult<InventoryOperationOutcome>> EquipActiveExpeditionRelicAsync(
            RoomInstanceId roomInstanceId, int backpackIndex, EquipmentSlot slot, TransactionId transactionId)
        {
            if (slot != EquipmentSlot.RelicOne && slot != EquipmentSlot.RelicTwo)
                return Task.FromResult(new ExpeditionMutationCommitResult<InventoryOperationOutcome>(
                    default, false, false, "La reliquia requiere uno de los dos espacios de reliquia."));

            string operationKey = "equip-relic:" + roomInstanceId + ":" +
                backpackIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)slot).ToString(CultureInfo.InvariantCulture) + ":" + transactionId;
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: false,
                (run, inventory, save) => inventory.EquipBackpackItem(backpackIndex, slot, transactionId),
                requiresSessionPublication: true);
        }
    }
}
