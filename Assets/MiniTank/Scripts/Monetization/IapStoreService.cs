#if MINITANK_IAP
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace MiniTank
{
    /// <summary>
    /// Google Play satın alma (Unity IAP 5). Altın paketleri tüketilebilir üründür.
    /// Ödeme onaylanınca önce altın hesaba yazılır, SONRA satın alma mağazaya "tamamlandı" bildirilir.
    /// Uygulama ödeme sırasında kapanırsa, yarım kalan satın alma bir sonraki açılışta tekrar gelir ve altın verilir.
    /// </summary>
    public class IapStoreService : IStoreService
    {
        StoreController store;
        readonly Dictionary<string, Product> products = new Dictionary<string, Product>();
        readonly Dictionary<string, TaskCompletionSource<bool>> waiting = new Dictionary<string, TaskCompletionSource<bool>>();
        readonly List<PendingOrder> unhandled = new List<PendingOrder>();
        bool connected;

        public bool Ready => connected && products.Count > 0;

        public string PriceOf(GoldPack pack)
        {
            Product p;
            if (products.TryGetValue(pack.productId, out p) && p != null && p.metadata != null &&
                !string.IsNullOrEmpty(p.metadata.localizedPriceString))
                return p.metadata.localizedPriceString;
            return pack.priceLabel;
        }

        public async void Initialize()
        {
            try
            {
                store = UnityIAPServices.StoreController();
                store.OnProductsFetched += OnProductsFetched;
                store.OnProductsFetchFailed += failed => Debug.LogWarning("[MiniTank] Ürünler alınamadı: " + failed.FailureReason);
                store.OnPurchasePending += OnPurchasePending;
                store.OnPurchaseFailed += OnPurchaseFailed;
                store.OnStoreDisconnected += d => { connected = false; Debug.LogWarning("[MiniTank] Mağaza bağlantısı koptu: " + d.message); };

                await store.Connect();
                connected = true;

                var defs = new List<ProductDefinition>();
                foreach (var pack in Store.Packs) defs.Add(new ProductDefinition(pack.productId, ProductType.Consumable));
                store.FetchProducts(defs);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MiniTank] Mağaza başlatılamadı: " + e.Message);
            }
        }

        void OnProductsFetched(List<Product> list)
        {
            foreach (var p in list)
                if (p != null && p.definition != null) products[p.definition.id] = p;
            // Önceki oturumdan yarım kalmış satın almaları da iste
            store.FetchPurchases();
        }

        public Task<bool> BuyAsync(GoldPack pack)
        {
            Product product;
            if (!connected || !products.TryGetValue(pack.productId, out product) || product == null)
                return Task.FromResult(false);
            if (waiting.ContainsKey(pack.productId)) return waiting[pack.productId].Task;

            var tcs = new TaskCompletionSource<bool>();
            waiting[pack.productId] = tcs;
            store.PurchaseProduct(product);
            return tcs.Task;
        }

        static string ProductIdOf(Order order)
        {
            var items = order != null && order.CartOrdered != null ? order.CartOrdered.Items() : null;
            if (items == null) return null;
            foreach (var item in items)
                if (item != null && item.Product != null && item.Product.definition != null) return item.Product.definition.id;
            return null;
        }

        void OnPurchasePending(PendingOrder order)
        {
            // İlerleme henüz yüklenmediyse (uygulama yeni açıldı) sonra işle
            if (!ProgressService.IsLoaded) { unhandled.Add(order); WaitForProgress(); return; }
            Grant(order);
        }

        bool waitingForProgress;

        async void WaitForProgress()
        {
            if (waitingForProgress) return;
            waitingForProgress = true;
            while (!ProgressService.IsLoaded) await Task.Delay(500);
            waitingForProgress = false;
            var list = new List<PendingOrder>(unhandled);
            unhandled.Clear();
            foreach (var o in list) Grant(o);
        }

        async void Grant(PendingOrder order)
        {
            string id = ProductIdOf(order);
            var pack = Store.Find(id);
            bool ok = false;
            if (pack != null)
            {
                try { ok = await ProgressService.AddGoldAsync(pack.gold); }
                catch (System.Exception e) { Debug.LogWarning("[MiniTank] Satın alınan altın yazılamadı: " + e.Message); }
            }
            else Debug.LogWarning("[MiniTank] Bilinmeyen ürün: " + id);

            // Altın yazıldıysa satın almayı tamamla (tamamlanmazsa Google Play 3 gün içinde iade eder,
            // uygulama da bir sonraki açılışta tekrar dener)
            if (ok) store.ConfirmPurchase(order);

            TaskCompletionSource<bool> tcs;
            if (id != null && waiting.TryGetValue(id, out tcs))
            {
                waiting.Remove(id);
                tcs.TrySetResult(ok);
            }
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            string id = ProductIdOf(order);
            Debug.Log("[MiniTank] Satın alma tamamlanmadı: " + order.FailureReason + " " + order.Details);
            TaskCompletionSource<bool> tcs;
            if (id != null && waiting.TryGetValue(id, out tcs))
            {
                waiting.Remove(id);
                tcs.TrySetResult(false);
            }
        }
    }
}
#endif
