// ====== CONFIG ======
const API_BASE = "http://localhost:5003"; // change if your backend port changes

// ====== Helpers ======
const $ = (id) => document.getElementById(id);

function moneyPHP(n) {
  const val = Number(n || 0);
  return "₱" + val.toFixed(2);
}

function getToken() {
  return localStorage.getItem("token") || "";
}
function setToken(t) {
  localStorage.setItem("token", t);
}
function clearToken() {
  localStorage.removeItem("token");
  localStorage.removeItem("email");
}

function authHeaders() {
  const token = getToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function api(path, opts = {}) {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(opts.headers || {}),
    },
    ...opts,
  });

  // try parse json if possible
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }

  if (!res.ok) {
    const msg = (data && (data.title || data.message || data)) || res.statusText;
    throw new Error(msg);
  }
  return data;
}

function toast(msg) {
  const t = $("toast");
  t.textContent = msg;
  t.classList.remove("hidden");
  setTimeout(() => t.classList.add("hidden"), 2200);
}

function requireLogin() {
  if (!getToken()) {
    openModal("authModal");
    toast("Please login first.");
    return false;
  }
  return true;
}

// ====== UI State ======
let products = [];
let cart = []; // UI cart mirror; actual source of truth is API cart
let orders = [];
let authMode = "login"; // login|register

// ====== Modals/Drawers ======
function openModal(id) { $(id).classList.remove("hidden"); }
function closeModal(id) { $(id).classList.add("hidden"); }

document.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-close]");
  if (btn) closeModal(btn.getAttribute("data-close"));
  if (e.target.id === "authModal") closeModal("authModal");
  if (e.target.id === "checkoutModal") closeModal("checkoutModal");
  if (e.target.id === "ordersModal") closeModal("ordersModal");
});

// ====== Auth ======
function updateAuthUI() {
  const email = localStorage.getItem("email");
  const token = getToken();
  $("authEmail").textContent = token ? email || "Logged in" : "Not logged in";
  $("btnOpenAuth").classList.toggle("hidden", !!token);
  $("btnLogout").classList.toggle("hidden", !token);
}

function setAuthMode(mode) {
  authMode = mode;
  $("tabLogin").classList.toggle("active", mode === "login");
  $("tabRegister").classList.toggle("active", mode === "register");
  $("btnAuthSubmit").textContent = mode === "login" ? "Login" : "Register";
  $("authMsg").textContent = "";
  $("authMsg").className = "msg";
}

$("btnOpenAuth").addEventListener("click", () => openModal("authModal"));
$("btnLogout").addEventListener("click", async () => {
  clearToken();
  updateAuthUI();
  toast("Logged out.");
});

$("tabLogin").addEventListener("click", () => setAuthMode("login"));
$("tabRegister").addEventListener("click", () => setAuthMode("register"));

$("btnAuthSubmit").addEventListener("click", async () => {
  const email = $("authEmailInput").value.trim();
  const password = $("authPassInput").value;

  if (!email || !password) {
    $("authMsg").textContent = "Email and password are required.";
    $("authMsg").className = "msg bad";
    return;
  }

  try {
    if (authMode === "register") {
      await api("/api/auth/register", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      });
      toast("Registered! Now login.");
      setAuthMode("login");
      return;
    }

    const out = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });

    setToken(out.token);
    localStorage.setItem("email", email);
    updateAuthUI();
    closeModal("authModal");
    toast("Logged in!");
    await refreshCart();
    await refreshOrdersCount();
  } catch (err) {
    $("authMsg").textContent = err.message;
    $("authMsg").className = "msg bad";
  }
});

// ====== Products ======
function renderProducts(list) {
  const grid = $("productsGrid");
  grid.innerHTML = "";
  $("emptyProducts").classList.toggle("hidden", list.length !== 0);

  list.forEach((p) => {
    const card = document.createElement("div");
    card.className = "product card lift bounce-hover";

    const stockBadge = p.stockQty > 0
      ? `<div class="badge ok">In stock: ${p.stockQty}</div>`
      : `<div class="badge out">Out of stock</div>`;

    card.innerHTML = `
      ${stockBadge}
      <div class="img"><span>📦</span></div>
      <h3>${escapeHtml(p.name)}</h3>
      <p>${escapeHtml(p.description || "No description.")}</p>
      <div class="row">
        <div>
          <div class="price">${moneyPHP(p.price)}</div>
          <div class="muted small">${escapeHtml(p.categoryName || "")}</div>
        </div>
        <button class="btn primary" ${p.stockQty <= 0 ? "disabled" : ""}>Add</button>
      </div>
    `;

    card.querySelector("button").addEventListener("click", async () => {
      if (!requireLogin()) return;
      try {
        await api(`/api/cart/add?productId=${p.id}&quantity=1`, {
          method: "POST",
          headers: { ...authHeaders() },
        });
        toast("Added to cart!");
        await refreshCart();
      } catch (err) {
        toast(err.message);
      }
    });

    grid.appendChild(card);
  });

  $("statProducts").textContent = String(list.length);
}

function escapeHtml(str) {
  return String(str).replace(/[&<>"']/g, (m) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;"
  }[m]));
}

async function refreshProducts() {
  $("statStatus").textContent = "Loading…";
  try {
    const list = await api("/api/products");
    products = list || [];
    applySearch();
    $("statStatus").textContent = "Ready";
  } catch (err) {
    $("statStatus").textContent = "Error";
    toast("Failed to load products: " + err.message);
  }
}

function applySearch() {
  const q = $("searchInput").value.trim().toLowerCase();
  const filtered = !q
    ? products
    : products.filter(p =>
        (p.name || "").toLowerCase().includes(q) ||
        (p.description || "").toLowerCase().includes(q) ||
        (p.categoryName || "").toLowerCase().includes(q)
      );
  renderProducts(filtered);
}

$("searchInput").addEventListener("input", applySearch);
$("btnRefresh").addEventListener("click", refreshProducts);

// ====== Cart ======
$("btnOpenCart").addEventListener("click", async () => {
  if (!requireLogin()) return;
  await refreshCart();
  openModal("cartDrawer");
});

$("btnClearCart").addEventListener("click", () => {
  // UI only; real cart is on server
  toast("Cart is server-based. Remove items using API next (optional).");
});

function renderCart(items, total) {
  const list = $("cartList");
  list.innerHTML = "";

  if (!items.length) {
    list.innerHTML = `<div class="empty">Your cart is empty.</div>`;
  } else {
    items.forEach((it) => {
      const row = document.createElement("div");
      row.className = "item bounce-hover";

      row.innerHTML = `
        <div class="row">
          <div>
            <div><b>${escapeHtml(it.productName || "Item")}</b></div>
            <div class="muted small">${moneyPHP(it.price)} × ${it.quantity}</div>
          </div>

          <div style="text-align:right">
            <div><b>${moneyPHP(it.lineTotal)}</b></div>
            <button class="btn tiny ghost" style="margin-top:8px">Remove</button>
          </div>
        </div>

        <div class="muted small">CartItemId: ${it.id}</div>
      `;

      // ✅ Remove button calls backend
      row.querySelector("button").addEventListener("click", async () => {
        try {
          await api(`/api/cart/${it.id}`, {
            method: "DELETE",
            headers: { ...authHeaders() }
          });
          toast("Removed from cart.");
          await refreshCart();
        } catch (err) {
          toast(err.message);
        }
      });

      list.appendChild(row);
    });
  }

  $("cartTotal").textContent = moneyPHP(total);
  $("cartCount").textContent = String(items.length);
  $("statCart").textContent = String(items.length);
}



async function refreshCart() {
  try {
    const out = await api("/api/cart", { headers: { ...authHeaders() } });
    cart = out.items || [];
    renderCart(out.items || [], out.total || 0);
  } catch (err) {
    // If not logged in, don't spam
  }
}

// ====== Checkout ======
$("btnCheckout").addEventListener("click", () => {
  if (!requireLogin()) return;
  openModal("checkoutModal");
});

$("btnPlaceOrder").addEventListener("click", async () => {
  const shippingName = $("shipName").value.trim();
  const shippingAddress = $("shipAddress").value.trim();
  const phone = $("shipPhone").value.trim();

  if (!shippingName || !shippingAddress || !phone) {
    $("checkoutMsg").textContent = "All fields are required.";
    $("checkoutMsg").className = "msg bad";
    return;
  }

  try {
    const out = await api("/api/cart/checkout", {
      method: "POST",
      headers: { ...authHeaders() , "Content-Type": "application/json"},
      body: JSON.stringify({
        shippingName,
        shippingAddress,
        phone,
        items: [] // server builds from cart
      })
    });

    $("checkoutMsg").textContent = `Order placed! ${out.orderNumber || ""}`;
    $("checkoutMsg").className = "msg ok";
    toast("Order placed!");
    closeModal("checkoutModal");
    closeModal("cartDrawer");

    await refreshCart();
    await loadMyOrders(true);
  } catch (err) {
    $("checkoutMsg").textContent = err.message;
    $("checkoutMsg").className = "msg bad";
  }
});

// ====== Orders ======
$("btnOpenOrders").addEventListener("click", async () => {
  if (!requireLogin()) return;
  await loadMyOrders(false);
  openModal("ordersModal");
});

async function refreshOrdersCount() {
  try {
    const my = await api("/api/orders/my", { headers: { ...authHeaders() } });
    orders = my || [];
    $("statOrders").textContent = String(orders.length);
  } catch {}
}

async function loadMyOrders(openModalAfter) {
  try {
    const my = await api("/api/orders/my", { headers: { ...authHeaders() } });
    orders = my || [];
    renderOrders(orders);
    $("statOrders").textContent = String(orders.length);

    if (openModalAfter) {
      openModal("ordersModal");
    }
  } catch (err) {
    toast(err.message);
  }
}

function renderOrders(list) {
  const box = $("ordersList");
  box.innerHTML = "";
  $("ordersEmpty").classList.toggle("hidden", list.length !== 0);

  list.forEach((o) => {
    const div = document.createElement("div");
    div.className = "order bounce-hover";

    const itemsHtml = (o.items || []).map(i => `
      <div class="order-item">
        <div>
          <div><b>${escapeHtml(i.productName || "Item")}</b></div>
          <div class="muted small">${moneyPHP(i.unitPrice)} × ${i.quantity}</div>
        </div>
        <div><b>${moneyPHP(i.lineTotal)}</b></div>
      </div>
    `).join("");

    div.innerHTML = `
      <div class="order-head">
        <div>
          <div class="order-num">${escapeHtml(o.orderNumber || ("Order #" + o.id))}</div>
          <div class="muted small">${new Date(o.createdAt).toLocaleString()}</div>
        </div>
        <div class="order-status">${escapeHtml(o.status)}</div>
      </div>

      <div class="row" style="margin-top:10px">
        <div class="muted small">${escapeHtml(o.shippingName)} • ${escapeHtml(o.phone)}</div>
        <div><b>${moneyPHP(o.totalAmount)}</b></div>
      </div>

      <div class="order-items">${itemsHtml}</div>

      <div class="row" style="margin-top:10px">
        <button class="btn primary">Pay (Simulated)</button>
        <div class="muted small">API: /api/orders/${o.id}/pay</div>
      </div>
    `;

    div.querySelector("button").addEventListener("click", async () => {
      if (!requireLogin()) return;
      try {
        await api(`/api/orders/${o.id}/pay`, {
          method: "POST",
          headers: { ...authHeaders() }
        });
        toast("Payment successful (simulated).");
        await loadMyOrders(false);
      } catch (err) {
        toast(err.message);
      }
    });

    box.appendChild(div);
  });
}

// ====== Init ======
updateAuthUI();
setAuthMode("login");
refreshProducts();

// If already logged in, refresh cart + orders count
if (getToken()) {
  refreshCart();
  refreshOrdersCount();
}
