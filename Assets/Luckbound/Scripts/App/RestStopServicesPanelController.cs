using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.Content;
using Luckbound.Core.Crafting;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Shows durable shop, smithing and rest-stop services only in a valid rest room.</summary>
    public sealed class RestStopServicesPanelController : MonoBehaviour
    {
        private enum ServiceTab { Shop, Smithing, Rest }

        private static readonly Color BackdropColor = new Color(0.025f, 0.02f, 0.018f, 0.88f);
        private static readonly Color PanelColor = new Color(0.15f, 0.105f, 0.08f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.31f, 0.22f, 0.16f, 1f);
        private static readonly Color DisabledButtonColor = new Color(0.18f, 0.16f, 0.14f, 1f);

        private ApplicationBootstrapper _app;
        private RecipeCatalog _recipes;
        private Button _accessButton;
        private GameObject _panelRoot;
        private RectTransform _contentRoot;
        private RectTransform _viewport;
        private Text _statusText;
        private Button _shopTabButton;
        private Button _smithingTabButton;
        private Button _restTabButton;
        private ScrollRect _scrollRect;
        private readonly Dictionary<string, Button> _pageButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private ServiceTab _selectedTab = ServiceTab.Rest;
        private string _statusMessage = string.Empty;
        private RoomInstanceId _availableRoomId;
        private bool _initialized;
        private bool _offered;
        private bool _busy;

        public bool IsServiceOffered => _offered && _accessButton != null && _accessButton.gameObject.activeInHierarchy;
        public bool IsPanelOpen => _panelRoot != null && _panelRoot.activeSelf;
        public bool IsBusy => _busy;
        public string StatusText => _statusText == null ? string.Empty : _statusText.text;
        public Button AccessButton => _accessButton;
        public TransactionId LastTransactionId { get; private set; }
        public string LastOperationKey { get; private set; } = string.Empty;

        public void Initialize(ApplicationBootstrapper app, Transform canvasRoot)
        {
            if (_initialized) throw new InvalidOperationException("Rest-stop services can only be initialized once.");
            if (canvasRoot == null) throw new ArgumentNullException(nameof(canvasRoot));

            _app = app;
            _initialized = true;
            LoadRecipes();
            CreateAccessButton(canvasRoot);
            CreatePanel(canvasRoot);
            RefreshAvailability(force: true);
        }

        private void Update()
        {
            if (!_initialized) return;
            RefreshAvailability(force: false);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !_offered) return;
            if (keyboard.eKey.wasPressedThisFrame && !IsPanelOpen) OpenPanel();
            else if (keyboard.escapeKey.wasPressedThisFrame && IsPanelOpen) ClosePanel();
        }

        private void OnDestroy()
        {
            if (EventSystem.current != null && _panelRoot != null &&
                EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_panelRoot.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void LoadRecipes()
        {
            try
            {
                TextAsset data = Resources.Load<TextAsset>("Luckbound/Data/Items/Recipes");
                if (data == null) throw new InvalidOperationException("Recipes.json could not be loaded.");
                ItemCatalog catalog = _app != null && _app.ActiveRun != null ? _app.ActiveRun.Inventory.Catalog : null;
                if (catalog == null) throw new InvalidOperationException("The active expedition item catalog is unavailable.");
                _recipes = RecipeCatalogJsonLoader.Load(data.text, catalog);
            }
            catch (Exception exception)
            {
                _statusMessage = L("No se pudo cargar la herrería: ", "Smithing data could not be loaded: ") + exception.Message;
            }
        }

        private void CreateAccessButton(Transform canvasRoot)
        {
            _accessButton = CreateButton(canvasRoot, "E03ServicesAccessButton",
                L("SERVICIOS · E", "SERVICES · E"), OpenPanel, ButtonColor, new Vector2(0f, -79f),
                new Vector2(186f, 26f), 13);
            _accessButton.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            _accessButton.gameObject.SetActive(false);
        }

        private void CreatePanel(Transform canvasRoot)
        {
            _panelRoot = CreateRect("E03ServicesPanel", canvasRoot, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(_panelRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            Image backdrop = _panelRoot.AddComponent<Image>();
            backdrop.color = BackdropColor;

            GameObject card = CreateRect("E03ServicesCard", _panelRoot.transform, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(450f, 244f), new Vector2(0.5f, 0.5f));
            card.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            Image cardImage = card.AddComponent<Image>();
            cardImage.color = PanelColor;

            CreateText("E03ServicesTitle", card.transform, L("SERVICIOS DEL DESCANSO", "REST STOP SERVICES"),
                new Vector2(0.5f, 0.5f), new Vector2(-45f, 105f), new Vector2(338f, 25f), 16, TextAnchor.MiddleLeft);
            CreateButton(card.transform, "E03ServicesCloseButton", L("CERRAR", "CLOSE"),
                ClosePanel, ButtonColor, new Vector2(194f, 105f), new Vector2(62f, 24f), 11);

            _shopTabButton = CreateButton(card.transform, "Tab_Shop", L("MERCADER", "MERCHANT"),
                () => SelectTab(ServiceTab.Shop), ButtonColor, new Vector2(-120f, 74f), new Vector2(120f, 24f), 12);
            _smithingTabButton = CreateButton(card.transform, "Tab_Smithing", L("HERRERÍA", "SMITHING"),
                () => SelectTab(ServiceTab.Smithing), ButtonColor, new Vector2(0f, 74f), new Vector2(120f, 24f), 12);
            _restTabButton = CreateButton(card.transform, "Tab_Rest", L("DESCANSO", "REST"),
                () => SelectTab(ServiceTab.Rest), ButtonColor, new Vector2(120f, 74f), new Vector2(120f, 24f), 12);

            GameObject viewportObject = CreateRect("E03ServicesViewport", card.transform, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(_viewport, new Vector2(14f, 43f), new Vector2(-14f, -63f));
            viewportObject.AddComponent<RectMask2D>();
            _scrollRect = viewportObject.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 20f;

            GameObject contentObject = CreateRect("E03ServicesContent", viewportObject.transform,
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f));
            _contentRoot = contentObject.GetComponent<RectTransform>();
            _contentRoot.anchorMin = new Vector2(0f, 1f);
            _contentRoot.anchorMax = new Vector2(1f, 1f);
            _contentRoot.pivot = new Vector2(0.5f, 1f);
            _contentRoot.anchoredPosition = Vector2.zero;
            _contentRoot.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter sizeFitter = contentObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.viewport = _viewport;
            _scrollRect.content = _contentRoot;

            _statusText = CreateText("E03ServicesStatus", card.transform, string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(420f, 26f), 11, TextAnchor.MiddleCenter);
            _panelRoot.SetActive(false);
        }

        private void RefreshAvailability(bool force)
        {
            bool available = IsCurrentRestRoom(_app);
            RoomInstanceId roomId = available ? _app.ActiveRoomInstanceId : default;
            bool roomChanged = roomId != _availableRoomId;
            if (!force && available == _offered && !roomChanged) return;

            _offered = available;
            _availableRoomId = roomId;
            if (_accessButton != null) _accessButton.gameObject.SetActive(available);
            if (!available && _panelRoot != null && _panelRoot.activeSelf) ClosePanel();
            else if (available && IsPanelOpen) RefreshVisiblePage(SelectedObjectName());
        }

        private void OpenPanel()
        {
            if (!IsCurrentRestRoom(_app))
            {
                RefreshAvailability(force: true);
                return;
            }

            _selectedTab = ServiceTab.Rest;
            _panelRoot.SetActive(true);
            RefreshVisiblePage(null);
            Select(_restTabButton);
        }

        private void ClosePanel()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (EventSystem.current != null)
            {
                GameObject target = _accessButton != null && _accessButton.gameObject.activeInHierarchy
                    ? _accessButton.gameObject
                    : null;
                EventSystem.current.SetSelectedGameObject(target);
            }
        }

        private void SelectTab(ServiceTab tab)
        {
            if (!IsCurrentRestRoom(_app))
            {
                RefreshAvailability(force: true);
                return;
            }

            _selectedTab = tab;
            RefreshVisiblePage(null);
            Select(GetTabButton(tab));
        }

        private void RefreshVisiblePage(string focusActionId)
        {
            if (_contentRoot == null || !IsPanelOpen) return;
            ExpeditionRunSessionPresenter presenter = GetComponent<ExpeditionRunSessionPresenter>();
            if (presenter != null) presenter.RefreshCurrentRunSummary();
            _pageButtons.Clear();
            for (int index = _contentRoot.childCount - 1; index >= 0; index--)
                Destroy(_contentRoot.GetChild(index).gameObject);

            if (!IsCurrentRestRoom(_app))
            {
                RefreshAvailability(force: true);
                return;
            }

            switch (_selectedTab)
            {
                case ServiceTab.Shop: BuildShopPage(); break;
                case ServiceTab.Smithing: BuildSmithingPage(); break;
                case ServiceTab.Rest: BuildRestPage(); break;
            }

            if (_statusText != null) _statusText.text = _statusMessage ?? string.Empty;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 1f;
            Button focus = null;
            if (!string.IsNullOrEmpty(focusActionId)) _pageButtons.TryGetValue(focusActionId, out focus);
            Select(focus != null && focus.interactable ? focus : GetTabButton(_selectedTab));
        }

        private void BuildShopPage()
        {
            ExpeditionInventory inventory = _app.ActiveRun.Inventory;
            AddInfo(L("SALDO · ", "BALANCE · ") + inventory.Gold + L(" oro", " gold"));
            MerchantStockSnapshot stock = _app.GetActiveMerchantStock();
            if (stock == null)
            {
                AddInfo(L("Entra en el descanso para fijar el stock de esta expedición.",
                    "Enter the rest stop to save this expedition's fixed stock."));
                return;
            }

            foreach (MerchantOfferSnapshot offer in stock.offers)
            {
                if (offer == null || !inventory.Catalog.TryGet(offer.itemId, out ItemDefinition definition)) continue;
                int price = MerchantPricing.GetPurchaseUnitPrice(definition, (ItemRarity)offer.rarity, 0);
                bool stocked = offer.remainingQuantity > 0;
                string label = stocked
                    ? L("COMPRAR x1 · ", "BUY 1 · ") + ItemLabel(offer.itemId) + " · " + price + L(" oro · stock ", " gold · stock ") + offer.remainingQuantity
                    : ItemLabel(offer.itemId) + L(" · AGOTADO", " · OUT OF STOCK");
                if (stocked && inventory.Gold < price) label += L(" · falta oro", " · not enough gold");
                string capturedItemId = offer.itemId;
                AddAction("Buy_" + StableUiId(capturedItemId), label,
                    () => Purchase(capturedItemId), stocked, height: 28f);
            }

            AddInfo(L("VENDER MATERIALES · precio unitario al 25 %", "SELL MATERIALS · unit value at 25 %"));
            foreach (MaterialKind materialKind in Enum.GetValues(typeof(MaterialKind)))
            {
                int count = inventory.GetMaterialCount(materialKind);
                ItemDefinition definition = inventory.Catalog.GetMaterial(materialKind);
                int value = MerchantPricing.GetSaleUnitPrice(definition, ItemRarity.Common);
                string label = count > 0
                    ? L("VENDER 1 · ", "SELL 1 · ") + MaterialLabel(materialKind) + " · +" + value + L(" oro · tienes ", " gold · held ") + count
                    : MaterialLabel(materialKind) + L(" · sin unidades", " · none held");
                MaterialKind capturedKind = materialKind;
                AddAction("SellMaterial_" + materialKind, label,
                    () => SellMaterial(capturedKind), count > 0, height: 28f);
            }

            AddInfo(L("VENDER OBJETOS · una unidad por acción", "SELL ITEMS · one unit per action"));
            foreach (InventoryItemStack item in EnumerateSellableItems(inventory))
            {
                if (!inventory.Catalog.TryGet(item.ItemId, out ItemDefinition definition)) continue;
                int value = MerchantPricing.GetSaleUnitPrice(definition, item.Rarity);
                string instanceId = item.InstanceId.ToString();
                string label = L("VENDER 1 · ", "SELL 1 · ") + ItemLabel(item.ItemId) +
                    " +" + item.UpgradeLevel + " · +" + value + L(" oro · tienes ", " gold · held ") + item.Quantity;
                ItemInstanceId capturedId = item.InstanceId;
                AddAction("SellItem_" + instanceId, label,
                    () => SellItem(capturedId), true, height: 28f);
            }
        }

        private void BuildSmithingPage()
        {
            ExpeditionInventory inventory = _app.ActiveRun.Inventory;
            AddInfo(L("MEJORAS CONSECUTIVAS · una instancia cada vez", "CONSECUTIVE UPGRADES · one instance at a time"));
            AddInfo(L("Bolsa · ", "Materials · ") + FormatMaterialBag(inventory) +
                " · " + inventory.Gold + L(" oro", " gold"));
            AddSmithingAction(inventory.Weapon, ItemKind.Weapon);
            AddSmithingAction(inventory.Armor, ItemKind.Armor);
            if (inventory.Weapon == null && inventory.Armor == null)
                AddInfo(L("No hay arma ni armadura que mejorar.", "There is no weapon or armor to upgrade."));
            if (_recipes == null && !string.IsNullOrEmpty(_statusMessage)) AddInfo(_statusMessage);
        }

        private void AddSmithingAction(InventoryItemStack target, ItemKind kind)
        {
            if (target == null)
            {
                AddInfo(kind == ItemKind.Weapon ? L("No hay arma equipada.", "No weapon is equipped.") :
                    L("No hay armadura equipada.", "No armor is equipped."));
                return;
            }

            if (target.UpgradeLevel >= 3)
            {
                AddInfo(ItemLabel(target.ItemId) + L(" · mejora máxima +3", " · maximum upgrade +3"));
                return;
            }

            string targetName = ItemLabel(target.ItemId);
            int nextLevel = target.UpgradeLevel + 1;
            string recipeId = "recipe.upgrade." + (kind == ItemKind.Weapon ? "weapon." : "armor.") + nextLevel;
            if (_recipes == null || !_recipes.TryGet(recipeId, out RecipeDefinition recipe))
            {
                AddInfo(targetName + L(" · receta de herrería no disponible.", " · smithing recipe unavailable."));
                return;
            }

            string cost = string.Join(" · ", recipe.MaterialCosts.Select(material =>
                material.Quantity.ToString(CultureInfo.InvariantCulture) + " " + MaterialLabel(material.MaterialKind)));
            if (recipe.GoldCost > 0) cost += " · " + recipe.GoldCost + L(" oro", " gold");
            string label = L("MEJORAR ", "UPGRADE ") + targetName + " +" + nextLevel + " · " + cost;
            ItemInstanceId targetId = target.InstanceId;
            AddAction("Upgrade_" + kind, label,
                () => Upgrade(recipeId, targetId), true, height: 31f);
        }

        private void BuildRestPage()
        {
            ExpeditionSaveData saved = _app.ActiveExpedition;
            ExpeditionInventory inventory = _app.ActiveRun.Inventory;
            MerchantStockSnapshot stock = _app.GetActiveMerchantStock();
            AddInfo(L("SALUD · ", "HEALTH · ") + saved.currentHealth + " / " + saved.maximumHealth +
                " · " + inventory.Gold + L(" oro", " gold"));

            if (stock == null)
            {
                AddInfo(L("El descanso recupera hasta 20 de salud y fija el stock una vez.",
                    "Resting restores up to 20 health and saves fixed stock once."));
                bool alive = saved.currentHealth > 0;
                AddAction("EnterRestStop", L("ENTRAR EN DESCANSO · recuperar hasta 20", "REST · restore up to 20"),
                    EnterRestStop, alive, height: 34f);
                return;
            }

            AddInfo(L("Descanso y stock guardados en esta sala.", "Rest and stock saved for this room."));
            RestStopUseSnapshot use = FindRestStopUse(inventory, _app.ActiveRoomInstanceId);
            bool alreadyUsed = use != null && use.taliaUsed;
            bool canUseTalia = !alreadyUsed && saved.currentHealth > 0 && saved.currentHealth < saved.maximumHealth &&
                inventory.Gold >= RestStopService.TaliaGoldCost;
            string taliaLabel = alreadyUsed
                ? L("TALIA · servicio ya utilizado", "TALIA · service already used")
                : L("TALIA · recuperar hasta 50 por 20 oro", "TALIA · restore up to 50 for 20 gold");
            if (!alreadyUsed && saved.currentHealth >= saved.maximumHealth) taliaLabel += L(" · salud completa", " · full health");
            else if (!alreadyUsed && inventory.Gold < RestStopService.TaliaGoldCost) taliaLabel += L(" · falta oro", " · not enough gold");
            AddAction("UseTalia", taliaLabel, UseTalia, canUseTalia, height: 34f);
        }

        private async Task EnterRestStop()
        {
            if (!CanOfferAction()) return;
            if (await RetryPendingBeforeNewAction()) return;
            _busy = true;
            SetStatus(L("GUARDANDO DESCANSO…", "SAVING REST STOP…"));
            try
            {
                ExpeditionMutationCommitResult<RestStopEntryCommitData> result =
                    await _app.EnterRestStopAsync(_app.ActiveRoomInstanceId);
                if (result.Persisted && result.Outcome != null && result.Outcome.RestOutcome.Succeeded)
                {
                    _statusMessage = L("Descanso guardado · +", "Rest saved · +") +
                        result.Outcome.RestOutcome.HealthRestored + L(" salud.", " health.");
                    _selectedTab = ServiceTab.Shop;
                }
                else if (result.Pending) SetStatus(PendingRetryText());
                else SetStatus(L("No se pudo entrar en el descanso · ", "Could not enter the rest stop · ") + result.Message);
            }
            catch (Exception exception) { SetStatus(L("No se pudo entrar en el descanso · ", "Could not enter the rest stop · ") + exception.Message); }
            finally { _busy = false; RefreshVisiblePage(SelectedObjectName()); }
        }

        private async Task Purchase(string itemId)
        {
            if (!CanOfferAction()) return;
            if (await RetryPendingBeforeNewAction()) return;
            _busy = true;
            LastTransactionId = TransactionId.New();
            LastOperationKey = string.Join("|", "merchant.buy", _app.ActiveRoomInstanceId, LastTransactionId,
                itemId, "1", "0");
            SetStatus(L("GUARDANDO COMPRA…", "SAVING PURCHASE…"));
            try
            {
                ExpeditionMutationCommitResult<EconomyTransactionOutcome> result = await _app.PurchaseFromMerchantAsync(
                    _app.ActiveRoomInstanceId, itemId, 1, 0, LastTransactionId);
                ShowEconomyResult(result.Persisted, result.Pending, result.Outcome, L("Compra", "Purchase"), result.Message);
            }
            catch (Exception exception) { SetStatus(L("No se pudo comprar · ", "Could not purchase · ") + exception.Message); }
            finally { _busy = false; RefreshVisiblePage(SelectedObjectName()); }
        }

        private async Task SellItem(ItemInstanceId itemInstanceId)
        {
            if (!CanOfferAction()) return;
            if (await RetryPendingBeforeNewAction()) return;
            _busy = true;
            LastTransactionId = TransactionId.New();
            LastOperationKey = string.Join("|", "merchant.sell-item", _app.ActiveRoomInstanceId, LastTransactionId,
                itemInstanceId, "1");
            SetStatus(L("GUARDANDO VENTA…", "SAVING SALE…"));
            try
            {
                ExpeditionMutationCommitResult<EconomyTransactionOutcome> result = await _app.SellItemToMerchantAsync(
                    _app.ActiveRoomInstanceId, itemInstanceId, 1, LastTransactionId);
                ShowEconomyResult(result.Persisted, result.Pending, result.Outcome, L("Venta", "Sale"), result.Message);
            }
            catch (Exception exception) { SetStatus(L("No se pudo vender · ", "Could not sell · ") + exception.Message); }
            finally { _busy = false; RefreshVisiblePage(SelectedObjectName()); }
        }

        private async Task SellMaterial(MaterialKind materialKind)
        {
            if (!CanOfferAction()) return;
            if (await RetryPendingBeforeNewAction()) return;
            _busy = true;
            LastTransactionId = TransactionId.New();
            LastOperationKey = string.Join("|", "merchant.sell-material", _app.ActiveRoomInstanceId, LastTransactionId,
                materialKind, "1");
            SetStatus(L("GUARDANDO VENTA…", "SAVING SALE…"));
            try
            {
                ExpeditionMutationCommitResult<EconomyTransactionOutcome> result = await _app.SellMaterialToMerchantAsync(
                    _app.ActiveRoomInstanceId, materialKind, 1, LastTransactionId);
                ShowEconomyResult(result.Persisted, result.Pending, result.Outcome, L("Venta", "Sale"), result.Message);
            }
            catch (Exception exception) { SetStatus(L("No se pudo vender · ", "Could not sell · ") + exception.Message); }
            finally { _busy = false; RefreshVisiblePage(SelectedObjectName()); }
        }

        private async Task Upgrade(string recipeId, ItemInstanceId targetInstanceId)
        {
            if (!CanOfferAction()) return;
            if (await RetryPendingBeforeNewAction()) return;
            _busy = true;
            LastTransactionId = TransactionId.New();
            LastOperationKey = string.Join("|", "smith.upgrade", _app.ActiveRoomInstanceId, LastTransactionId,
                recipeId, targetInstanceId);
            SetStatus(L("GUARDANDO MEJORA…", "SAVING UPGRADE…"));
            try
            {
                ExpeditionMutationCommitResult<SmithingOutcome> result = await _app.UpgradeEquipmentAsync(
                    _app.ActiveRoomInstanceId, recipeId, targetInstanceId, LastTransactionId);
                if (result.Pending) SetStatus(PendingRetryText());
                else if (!result.Persisted) SetStatus(L("No se pudo guardar la mejora · ", "Could not save upgrade · ") + result.Message);
                else if (result.Outcome != null && result.Outcome.Succeeded)
                    SetStatus(L("Mejora guardada.", "Upgrade saved."));
                else SetStatus(L("Mejora rechazada · ", "Upgrade rejected · ") +
                    (result.Outcome?.CraftingOutcome?.Status.ToString() ?? result.Message));
            }
            catch (Exception exception) { SetStatus(L("No se pudo mejorar · ", "Could not upgrade · ") + exception.Message); }
            finally { _busy = false; RefreshVisiblePage(SelectedObjectName()); }
        }

        private async Task UseTalia()
        {
            if (!CanOfferAction()) return;
            if (await RetryPendingBeforeNewAction()) return;
            _busy = true;
            LastTransactionId = TransactionId.New();
            LastOperationKey = string.Join("|", "rest.talia", _app.ActiveRoomInstanceId, LastTransactionId);
            SetStatus(L("GUARDANDO SERVICIO DE TALIA…", "SAVING TALIA'S SERVICE…"));
            try
            {
                ExpeditionMutationCommitResult<EconomyTransactionOutcome> result = await _app.UseTaliaAsync(
                    _app.ActiveRoomInstanceId, LastTransactionId);
                ShowEconomyResult(result.Persisted, result.Pending, result.Outcome, L("Servicio de Talia", "Talia's service"), result.Message);
            }
            catch (Exception exception) { SetStatus(L("No se pudo usar el servicio de Talia · ", "Could not use Talia's service · ") + exception.Message); }
            finally { _busy = false; RefreshVisiblePage(SelectedObjectName()); }
        }

        private async Task<bool> RetryPendingBeforeNewAction()
        {
            if (_app.HasPendingEconomyMutation)
            {
                _busy = true;
                SetStatus(PendingRetryText());
                string key = _app.PendingEconomyOperationKey;
                try
                {
                    if (key.StartsWith("rest.enter|", StringComparison.Ordinal))
                    {
                        ExpeditionMutationCommitResult<RestStopEntryCommitData> retried =
                            await _app.RetryPendingEconomyMutationAsync<RestStopEntryCommitData>(key);
                        _statusMessage = FormatRestEntryRetry(retried);
                    }
                    else if (key.StartsWith("smith.upgrade|", StringComparison.Ordinal))
                    {
                        ExpeditionMutationCommitResult<SmithingOutcome> retried =
                            await _app.RetryPendingEconomyMutationAsync<SmithingOutcome>(key);
                        _statusMessage = FormatSmithingRetry(retried);
                    }
                    else if (key.StartsWith("merchant.buy|", StringComparison.Ordinal) ||
                        key.StartsWith("merchant.sell-item|", StringComparison.Ordinal) ||
                        key.StartsWith("merchant.sell-material|", StringComparison.Ordinal) ||
                        key.StartsWith("rest.talia|", StringComparison.Ordinal))
                    {
                        ExpeditionMutationCommitResult<EconomyTransactionOutcome> retried =
                            await _app.RetryPendingEconomyMutationAsync<EconomyTransactionOutcome>(key);
                        _statusMessage = FormatEconomyRetry(retried);
                    }
                    else _statusMessage = L("Hay otro guardado pendiente; no se inició una acción nueva.",
                        "Another save is pending; no new action was started.");
                }
                catch (Exception exception)
                {
                    _statusMessage = PendingRetryText() + " " + exception.Message;
                }
                finally
                {
                    _busy = false;
                    RefreshVisiblePage(SelectedObjectName());
                }
                return true;
            }

            if (_app.HasPendingExpeditionWrite)
            {
                _busy = true;
                SetStatus(L("REINTENTANDO EL GUARDADO ANTERIOR…", "RETRYING THE PREVIOUS SAVE…"));
                try
                {
                    SaveWriteResult save = await _app.RetryPendingExpeditionWriteAsync();
                    _statusMessage = save.Succeeded
                        ? L("Guardado anterior confirmado · vuelve a pulsar para continuar.",
                            "Previous save confirmed · press again to continue.")
                        : PendingRetryText() + " " + save.Message;
                }
                catch (Exception exception) { _statusMessage = PendingRetryText() + " " + exception.Message; }
                finally
                {
                    _busy = false;
                    RefreshVisiblePage(SelectedObjectName());
                }
                return true;
            }

            return false;
        }

        private void ShowEconomyResult(bool persisted, bool pending, EconomyTransactionOutcome outcome, string operation, string message)
        {
            if (pending) SetStatus(PendingRetryText());
            else if (!persisted) SetStatus(operation + L(" no guardada · ", " not saved · ") + message);
            else if (outcome != null && outcome.Succeeded)
                SetStatus(operation + L(" guardada · saldo ", " saved · balance ") + _app.ActiveRun.Inventory.Gold +
                    L(" oro.", " gold."));
            else SetStatus(operation + L(" rechazada · ", " rejected · ") + (outcome == null ? message : outcome.Status.ToString()));
        }

        private string FormatRestEntryRetry(ExpeditionMutationCommitResult<RestStopEntryCommitData> result)
        {
            if (result.Pending) return PendingRetryText();
            if (!result.Persisted || result.Outcome == null) return L("El descanso sigue pendiente · ", "Rest stop is still pending · ") + result.Message;
            if (!result.Outcome.RestOutcome.Succeeded) return L("Descanso rechazado · ", "Rest stop rejected · ") + result.Outcome.RestOutcome.Status;
            _selectedTab = ServiceTab.Shop;
            return L("Descanso guardado · +", "Rest saved · +") + result.Outcome.RestOutcome.HealthRestored + L(" salud.", " health.");
        }

        private string FormatSmithingRetry(ExpeditionMutationCommitResult<SmithingOutcome> result)
        {
            if (result.Pending) return PendingRetryText();
            if (!result.Persisted || result.Outcome == null) return L("La mejora sigue pendiente · ", "Upgrade is still pending · ") + result.Message;
            return result.Outcome.Succeeded ? L("Mejora guardada.", "Upgrade saved.") :
                L("Mejora rechazada · ", "Upgrade rejected · ") + result.Outcome.CraftingOutcome.Status;
        }

        private string FormatEconomyRetry(ExpeditionMutationCommitResult<EconomyTransactionOutcome> result)
        {
            if (result.Pending) return PendingRetryText();
            if (!result.Persisted || result.Outcome == null) return L("La operación sigue pendiente · ", "Operation is still pending · ") + result.Message;
            return result.Outcome.Succeeded ? L("Operación guardada · saldo ", "Operation saved · balance ") +
                _app.ActiveRun.Inventory.Gold + L(" oro.", " gold.") :
                L("Operación rechazada · ", "Operation rejected · ") + result.Outcome.Status;
        }

        private bool CanOfferAction()
        {
            if (_busy) return false;
            if (IsCurrentRestRoom(_app)) return true;
            RefreshAvailability(force: true);
            return false;
        }

        private static bool IsCurrentRestRoom(ApplicationBootstrapper app)
        {
            if (app == null || !app.SaveDataReady || !app.HasActiveExpedition || app.ActiveRun == null ||
                !string.IsNullOrEmpty(app.ActiveExpeditionLoadError) || !app.ActiveRoomInstanceId.IsValid) return false;
            ExpeditionSaveData saved = app.ActiveExpedition;
            if (saved == null || string.IsNullOrWhiteSpace(saved.currentNodeId)) return false;
            foreach (ExpeditionRouteNode candidate in app.ActiveRun.Route.Nodes)
            {
                if (candidate != null && string.Equals(candidate.NodeId, saved.currentNodeId, StringComparison.Ordinal))
                    return candidate.Kind == ExpeditionRouteNodeKind.Rest;
            }
            return false;
        }

        private static RestStopUseSnapshot FindRestStopUse(ExpeditionInventory inventory, RoomInstanceId roomId)
        {
            return inventory.CreateSnapshot().restStops?.FirstOrDefault(entry => entry != null &&
                string.Equals(entry.roomInstanceId, roomId.ToString(), StringComparison.Ordinal));
        }

        private static IEnumerable<InventoryItemStack> EnumerateSellableItems(ExpeditionInventory inventory)
        {
            var seen = new HashSet<ItemInstanceId>();
            InventoryItemStack[] equipped = { inventory.Weapon, inventory.Armor, inventory.RelicOne, inventory.RelicTwo };
            foreach (InventoryItemStack item in equipped)
                if (item != null && seen.Add(item.InstanceId)) yield return item;
            foreach (InventoryItemStack item in inventory.Backpack)
                if (item != null && seen.Add(item.InstanceId)) yield return item;
        }

        private void AddInfo(string text)
        {
            GameObject row = CreateRect("Info_" + _contentRoot.childCount.ToString(CultureInfo.InvariantCulture), _contentRoot,
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f));
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 22f);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 22f;
            Text label = row.AddComponent<Text>();
            label.font = BuiltinFont();
            label.fontSize = 12;
            label.color = new Color(0.93f, 0.88f, 0.79f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
        }

        private void AddAction(string id, string label, Action action, bool interactable, float height)
        {
            Button button = CreateButton(_contentRoot, id, label, action,
                interactable ? ButtonColor : DisabledButtonColor, Vector2.zero, Vector2.zero, 11);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, height);
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            button.interactable = interactable;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            _pageButtons[id] = button;
        }

        private Button CreateButton(Transform parent, string name, string label, Action action, Color color,
            Vector2 anchoredPosition, Vector2 size, int fontSize)
        {
            GameObject buttonObject = CreateRect(name, parent, new Vector2(0.5f, 0.5f), anchoredPosition,
                size, new Vector2(0.5f, 0.5f));
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() => action?.Invoke());
            Text text = CreateText(name + "Label", buttonObject.transform, label,
                Vector2.zero, Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, new Vector2(4f, 1f), new Vector2(-4f, -1f));
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(string name, Transform parent, string value, Vector2 anchor,
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = CreateRect(name, parent, anchor, position, size, new Vector2(0.5f, 0.5f));
            Text text = textObject.AddComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, Vector2 pivot)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.pivot = pivot;
            return item;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Font BuiltinFont() => Resources.GetBuiltinResource<Font>("Arial.ttf");

        private void Select(Button button)
        {
            if (button != null && EventSystem.current != null && button.gameObject.activeInHierarchy && button.interactable)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private string SelectedObjectName()
        {
            return EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null
                ? string.Empty
                : EventSystem.current.currentSelectedGameObject.name;
        }

        private Button GetTabButton(ServiceTab tab)
        {
            switch (tab)
            {
                case ServiceTab.Shop: return _shopTabButton;
                case ServiceTab.Smithing: return _smithingTabButton;
                default: return _restTabButton;
            }
        }

        private void SetStatus(string value)
        {
            _statusMessage = value ?? string.Empty;
            if (_statusText != null) _statusText.text = _statusMessage;
        }

        private string FormatMaterialBag(ExpeditionInventory inventory)
        {
            return string.Join(" · ", Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select(kind => MaterialLabel(kind) + " " + inventory.GetMaterialCount(kind)));
        }

        private string ItemLabel(string itemId)
        {
            switch (itemId)
            {
                case "consumable.health_potion": return L("Poción de salud", "Health potion");
                case "consumable.energy_tonic": return L("Tónico de energía", "Energy tonic");
                case "consumable.antidote": return L("Antídoto", "Antidote");
                case "consumable.bomb": return L("Bomba", "Bomb");
                case "consumable.fortune_brew": return L("Brebaje de Fortuna", "Fortune brew");
                case "consumable.torch": return L("Antorcha", "Torch");
                case "relic.marked_coin": return L("Moneda marcada", "Marked coin");
                case "relic.hidden_pouch": return L("Bolsa oculta", "Hidden pouch");
                case "material.shadow_wood": return L("madera", "wood");
                case "material.ore": return L("mineral", "ore");
                case "material.umbra_herb": return L("hierba", "herb");
                case "material.arcane_dust": return L("polvo arcano", "arcane dust");
                case "material.dark_resin": return L("resina oscura", "dark resin");
                case "material.vital_ember": return L("ascua vital", "vital ember");
                case "weapon.bellator.rusted_sword": return L("Espada gastada", "Rusted sword");
                case "weapon.venator.short_bow": return L("Arco corto", "Short bow");
                case "weapon.magus.ember_staff": return L("Báculo de ascua", "Ember staff");
                case "weapon.bellator.trail_saber": return L("Sable del sendero", "Trail saber");
                case "weapon.bellator.umbra_greatsword": return L("Mandoble de Umbra", "Umbra greatsword");
                case "weapon.venator.long_bow": return L("Arco largo", "Long bow");
                case "weapon.venator.ruins_crossbow": return L("Ballesta de ruinas", "Ruins crossbow");
                case "weapon.magus.frost_staff": return L("Báculo de escarcha", "Frost staff");
                case "weapon.magus.shadow_staff": return L("Báculo de sombra", "Shadow staff");
                case "armor.bellator.guild_cuirass": return L("Cota del gremio", "Guild cuirass");
                case "armor.venator.leather_vest": return L("Chaleco de cuero", "Leather vest");
                case "armor.magus.traveler_robe": return L("Túnica de viajero", "Traveler robe");
                default: return itemId.Replace("weapon.", string.Empty).Replace("armor.", string.Empty)
                    .Replace("relic.", string.Empty).Replace("consumable.", string.Empty).Replace('_', ' ');
            }
        }

        private string MaterialLabel(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.ShadowWood: return L("madera", "wood");
                case MaterialKind.Ore: return L("mineral", "ore");
                case MaterialKind.UmbraHerb: return L("hierba", "herb");
                case MaterialKind.ArcaneDust: return L("polvo", "dust");
                case MaterialKind.DarkResin: return L("resina", "resin");
                case MaterialKind.VitalEmber: return L("ascua vital", "vital ember");
                default: return kind.ToString();
            }
        }

        private static string StableUiId(string value) => value.Replace('.', '_').Replace('/', '_');

        private string L(string spanish, string english)
        {
            return _app != null && _app.Settings != null && _app.Settings.languageId == "en" ? english : spanish;
        }
    }
}
