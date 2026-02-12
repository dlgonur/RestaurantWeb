console.log("siparis-take.js loaded");

(() => {
    const siparisIdEl = document.getElementById("siparisId");
    const productsEl = document.getElementById("products");
    const cartEl = document.getElementById("cart");
    const subtotalEl = document.getElementById("subtotal");
    const totalEl = document.getElementById("total");
    const discountEl = document.getElementById("discount");
    const statusEl = document.getElementById("status");
    const btnSubmit = document.getElementById("btnSubmit");
    const btnRefresh = document.getElementById("btnRefresh");
    const searchEl = document.getElementById("productSearch");
    const btnClose = document.getElementById("btnClose");
    const kategoriSelect = document.getElementById("kategoriSelect");
    const paymentMethodEl = document.getElementById("paymentMethod");

    if (!siparisIdEl) return;

    const siparisId = parseInt(siparisIdEl.innerText, 10);

    // Anti-forgery token
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const antiForgeryToken = tokenInput ? tokenInput.value : "";

    // cart state: { [urunId]: { urunId, ad, fiyat, adet, stok } }
    const cart = {};
    let productState = [];

    function showToast(type, message, delay = 2500) {
        // type: "success" | "danger" | "info"
        const containerId = "appToastContainer";

        let container = document.getElementById(containerId);
        if (!container) {
            container = document.createElement("div");
            container.id = containerId;
            container.className = "toast-container position-fixed top-0 end-0 p-3";
            container.style.zIndex = "1100";
            document.body.appendChild(container);
        }

        const toastEl = document.createElement("div");
        toastEl.className = `toast align-items-center text-bg-${type} border-0`;
        toastEl.setAttribute("role", "alert");
        toastEl.setAttribute("aria-live", "assertive");
        toastEl.setAttribute("aria-atomic", "true");

        toastEl.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;

        container.appendChild(toastEl);

        const t = new bootstrap.Toast(toastEl, { delay });
        t.show();

        toastEl.addEventListener("hidden.bs.toast", () => toastEl.remove());
    }


    function money(n) { return Number(n).toFixed(2); }

    function calcTotals() {
        let subtotal = 0;

        for (const k in cart) {
            const fiyat = Number(cart[k].fiyat);
            const adet = Number(cart[k].adet);
            if (!Number.isFinite(fiyat) || !Number.isFinite(adet)) continue;
            subtotal += fiyat * adet;
        }

        let oran = parseDiscount(discountEl.value);

        const total = subtotal - (subtotal * (oran / 100)); 

        subtotalEl.innerText = money(subtotal);
        totalEl.innerText = money(total); 
    }


    async function refreshSavedOrders() {
        const container = document.getElementById("savedOrderContainer");
        if (!container) return;

        const res = await fetch(`/Siparisler/GetOrderTable?siparisId=${siparisId}`);
        if (!res.ok) return;

        const html = await res.text();
        container.innerHTML = html;
    }

    function renderCart() {
        const keys = Object.keys(cart);
        if (keys.length === 0) {
            cartEl.innerHTML = '<div class="text-muted">Sepet boş.</div>';
            calcTotals();
            return;
        }

        cartEl.innerHTML = keys.map(k => {
            const it = cart[k];
            return `
      <div class="list-group-item">
        <div class="d-flex justify-content-between align-items-center">
          <div>
            <div><strong>${it.ad}</strong></div>
            <div class="text-muted">Fiyat: ${money(it.fiyat)} | Stok: ${it.stok}</div>
          </div>
          <button class="btn btn-sm btn-outline-danger" data-remove="${it.urunId}">Sil</button>
        </div>

        <div class="d-flex justify-content-between align-items-center mt-2">
          <div class="input-group input-group-sm" style="max-width:140px">
            <button class="btn btn-outline-secondary" data-dec="${it.urunId}">-</button>
            <input class="form-control text-center" value="${it.adet}" readonly />
            <button class="btn btn-outline-secondary" data-inc="${it.urunId}">+</button>
          </div>
          <div>
            Satır: <strong>${money(it.fiyat * it.adet)}</strong>
          </div>
        </div>
      </div>`;
        }).join("");

        calcTotals();
    }

    function renderProducts(list) {
        if (!list || list.length === 0) {
            productsEl.innerHTML = '<div class="text-muted">Ürün bulunamadı.</div>';
            return;
        }

        productsEl.innerHTML = list.map(p => {
            const disabled = p.stok <= 0 ? 'disabled' : '';
            return `
      <button class="list-group-item list-group-item-action d-flex justify-content-between align-items-center"
              data-add="${p.id}" ${disabled}>
        <div>
          <div><strong>${p.ad}</strong></div>
          <div class="text-muted">${p.kategori} | Fiyat: ${money(p.fiyat)} | Stok: ${p.stok}</div>
        </div>
        <span class="badge bg-primary rounded-pill">Ekle</span>
      </button>`;
        }).join("");
    }

    function applyProductFilter() {
        const q = (searchEl.value || "").trim().toLowerCase();
        if (!q) { renderProducts(productState); return; }

        const starts = productState.filter(p => (p.ad || "").toLowerCase().startsWith(q));
        const contains = productState.filter(p =>
            !(p.ad || "").toLowerCase().startsWith(q) &&
            (p.ad || "").toLowerCase().includes(q)
        );

        renderProducts([...starts, ...contains]);
    }

    function setProductState(list) {
        productState = list || [];
        applyProductFilter();
    }

    async function loadProductsState(kategoriId = "") {
        const qs = kategoriId ? `?kategoriId=${encodeURIComponent(kategoriId)}` : "";
        statusEl.innerText = "Ürünler yükleniyor...";

        try {
            const res = await fetch(`/Siparisler/Products${qs}`);
            if (!res.ok) { statusEl.innerText = "Ürünler yüklenemedi."; return; }
            const list = await res.json();
            setProductState(list);
            statusEl.innerText = "";
        } catch {
            statusEl.innerText = "Ürünler yüklenemedi (bağlantı hatası).";
        }
    }

    async function loadCategories() {
        if (!kategoriSelect) return;

        try {
            const res = await fetch('/Siparisler/Categories');
            if (!res.ok) return;

            const cats = await res.json();
            kategoriSelect.innerHTML = `
                <option value="">Tümü</option>
                ${cats.map(c => `<option value="${c.id}">${c.ad}</option>`).join("")}
            `;
        } catch {
            // sessiz geç
        }
    }

    // --- Events ---

    if (kategoriSelect) {
        kategoriSelect.addEventListener('change', async () => {
            const kid = kategoriSelect.value || "";
            await loadProductsState(kid);
        });
    }

    btnRefresh.addEventListener('click', () => {
        const kid = kategoriSelect ? (kategoriSelect.value || "") : "";
        loadProductsState(kid);
    });

    searchEl.addEventListener('input', applyProductFilter);

    productsEl.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-add]');
        if (!btn) return;

        const urunId = parseInt(btn.getAttribute('data-add'), 10);
        const p = productState.find(x => x.id === urunId);
        if (!p || p.stok <= 0) return;

        if (!cart[urunId]) {
            cart[urunId] = { urunId: p.id, ad: p.ad, fiyat: p.fiyat, adet: 1, stok: p.stok };
        } else {
            if (cart[urunId].adet + 1 > p.stok) return;
            cart[urunId].adet += 1;
        }

        renderCart();
    });

    cartEl.addEventListener('click', (e) => {
        const inc = e.target.closest('[data-inc]');
        const dec = e.target.closest('[data-dec]');
        const rem = e.target.closest('[data-remove]');

        if (inc) {
            const id = parseInt(inc.getAttribute('data-inc'), 10);
            const p = productState.find(x => x.id === id);
            if (!p) return;
            if (cart[id].adet + 1 > p.stok) return;
            cart[id].adet += 1;
            renderCart();
        }
        if (dec) {
            const id = parseInt(dec.getAttribute('data-dec'), 10);
            cart[id].adet -= 1;
            if (cart[id].adet <= 0) delete cart[id];
            renderCart();
        }
        if (rem) {
            const id = parseInt(rem.getAttribute('data-remove'), 10);
            delete cart[id];
            renderCart();
        }
    });

    function normalizeDiscountText(s) {
        s = String(s ?? "").trim();

        // sadece rakam ve tek ayraç (nokta/virgül) kalsın
        s = s.replace(/[^\d.,]/g, "");

        // ilk ayraçtan sonrakilerde nokta/virgülleri kaldır (tek ayraç kuralı)
        const idx = s.search(/[.,]/);
        if (idx !== -1) {
            const head = s.slice(0, idx + 1);
            const tail = s.slice(idx + 1).replace(/[.,]/g, "");
            s = head + tail;
        }

        return s;
    }
    function parseDiscount(s) {
        s = String(s ?? "").trim().replace(",", ".");
        if (s === "") return 0;

        const n = Number(s);
        if (!Number.isFinite(n)) return 0;

        return Math.max(0, Math.min(100, n));
    }

    // yazarken engelle (eksi, e/E, +)
    discountEl.addEventListener("keydown", (e) => {
        if (["e", "E", "-", "+"].includes(e.key)) {
            e.preventDefault();
        }
    });

    // yazarken sanitize + clamp
    discountEl.addEventListener("input", () => {
        const cleaned = normalizeDiscountText(discountEl.value);
        if (cleaned !== discountEl.value) {
            discountEl.value = cleaned;
        }

        const v = parseDiscount(discountEl.value);
        if (discountEl.value !== "" && String(v) !== discountEl.value.replace(",", ".")) {
            discountEl.value = String(v);
        }

        calcTotals();
    });


    discountEl.addEventListener('change', async () => {

        let oran = parseDiscount(discountEl.value);

        statusEl.innerText = "İskonto güncelleniyor...";

        try {
            const res = await fetch('/Siparisler/UpdateDiscount', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken
                },
                body: JSON.stringify({ siparisId: siparisId, iskontoOran: oran })
            });

            const raw = await res.text();
            let data;
            try { data = JSON.parse(raw); } catch { data = { message: raw }; }

            if (!res.ok) {
                statusEl.innerText = data?.message ?? "İskonto güncellenemedi.";
                return;
            }

            statusEl.innerText = "";
            calcTotals();
            await refreshSavedOrders();
        }
        catch {
            statusEl.innerText = "İskonto güncellenemedi (bağlantı hatası).";
        }
    });

    btnSubmit.addEventListener('click', async () => {
        const keys = Object.keys(cart);
        if (keys.length === 0) { statusEl.innerText = "Sepet boş."; return; }

        btnSubmit.disabled = true;
        statusEl.innerText = "Kaydediliyor...";

        const payload = {
            siparisId: siparisId,
            items: keys.map(k => ({ urunId: parseInt(k, 10), adet: cart[k].adet }))
        };

        try {
            const res = await fetch('/Siparisler/Submit', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken
                },
                body: JSON.stringify(payload)
            });

            const raw = await res.text();
            let data;
            try { data = JSON.parse(raw); } catch { data = { message: raw }; }

            if (!res.ok) {
                statusEl.innerText = data?.message ?? "Kaydetme başarısız.";
                btnSubmit.disabled = false;
                return;
            }

            // ürünleri seçili kategoriye göre yenile
            const kid = kategoriSelect ? (kategoriSelect.value || "") : "";
            await loadProductsState(kid);

            // sepet temizle
            for (const k of Object.keys(cart)) delete cart[k];
            renderCart();

            // adisyonu yenile
            await refreshSavedOrders();

            showToast("success", data?.message ?? "Sepetteki ürünler adisyona eklendi.");

            statusEl.innerText = "";
            btnSubmit.disabled = false;
        } catch (err) {
            console.error(err);
            statusEl.innerText = "Sunucuya bağlanılamadı.";
            btnSubmit.disabled = false;
        }
    });

    if (btnClose) {
        btnClose.addEventListener('click', async () => {
            btnClose.disabled = true;
            statusEl.innerText = "Kapatılıyor...";

            const yontem = paymentMethodEl ? parseInt(paymentMethodEl.value, 10) : 1; 

            try {
                const res = await fetch('/Siparisler/Close', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': antiForgeryToken
                    },
                    body: JSON.stringify({ siparisId: siparisId, yontem }) 
                });

                const raw = await res.text();
                let data;
                try { data = JSON.parse(raw); } catch { data = { message: raw }; }

                if (!res.ok) {
                    statusEl.innerText = data?.message ?? "Kapatma başarısız.";
                    btnClose.disabled = false;
                    return;
                }

                window.location.href = "/Masalar/Board";
            }
            catch {
                statusEl.innerText = "Bağlantı hatası.";
                btnClose.disabled = false;
            }
        });
    }

    // init
    renderCart();
    loadCategories().then(() => {
        const kid = kategoriSelect ? (kategoriSelect.value || "") : "";
        loadProductsState(kid);
    });
})();
