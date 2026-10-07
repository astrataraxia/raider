// 즐겨찾기 목록을 그리고, 토글과 카테고리 이동을 서버에 반영한다.
import { setFavoriteCategory, favoriteKey, listFavorites, readToken, toggleFavorite } from "#favorites-api";
import { bindSidebar } from "#favorites-sidebar";
import { readJson, storageKeys, writeJson } from "#storage";

export function startFavorites() {
  const sidebar = document.getElementById("favorites-sidebar");
  const list = document.getElementById("favorites-list");
  const status = document.querySelector(".favorites-status");
  const collapse = document.querySelector(".favorites-collapse");
  const mobileToggle = document.querySelector(".favorites-mobile-toggle");
  const backdrop = document.querySelector(".favorites-backdrop");
  const pageShell = document.querySelector(".page-shell");
  const siteHeader = document.querySelector(".site-header");
  const token = readToken();
  const favorites = new Map();
  let afterWrite = null;
  let onLoaded = null;

  if (!sidebar || !list || !status || !collapse || !mobileToggle || !backdrop || !pageShell || !siteHeader || !token) {
    return {
      async refresh() {},
      setAfterWrite() {},
      setOnLoaded() {},
    };
  }

  bindSidebar({ sidebar, collapse, mobileToggle, backdrop, pageShell, siteHeader });

  function readList(key) {
    const value = readJson(key, []);
    return Array.isArray(value) ? value : [];
  }

  function bindDrop(target, className, onDrop) {
    target.addEventListener("dragover", (event) => {
      event.preventDefault();
      target.classList.add(className);
    });
    target.addEventListener("dragenter", (event) => {
      event.preventDefault();
      target.classList.add(className);
    });
    target.addEventListener("dragleave", () => {
      target.classList.remove(className);
    });
    target.addEventListener("drop", (event) => {
      event.preventDefault();
      target.classList.remove(className);
      onDrop(event);
    });
  }

  function render() {
    list.replaceChildren();
    document.querySelectorAll(".favorite-toggle").forEach((button) => {
      const card = button.closest(".stream-card");
      const favorite = favorites.has(favoriteKey(card.dataset.platform, card.dataset.channelId));
      button.classList.toggle("is-favorite", favorite);
      button.setAttribute("aria-pressed", favorite ? "true" : "false");
      button.setAttribute("aria-label", `${card.dataset.streamerName}${favorite ? " 즐겨찾기 제거" : " 즐겨찾기 추가"}`);
      button.setAttribute("title", favorite ? "즐겨찾기 제거" : "즐겨찾기 추가");
    });

    const customCategories = readList(storageKeys.customCategories);
    if (favorites.size === 0 && customCategories.length === 0) {
      status.textContent = "즐겨찾기한 방송인이 없습니다.";
      return;
    }

    status.textContent = `${favorites.size}명`;
    const grouped = {};
    const allCategories = new Set(["기본"]);
    customCategories.forEach((cat) => {
      allCategories.add(cat);
      if (!grouped[cat]) {
        grouped[cat] = [];
      }
    });
    favorites.forEach((fav) => {
      const cat = fav.category || "기본";
      allCategories.add(cat);
      if (!grouped[cat]) {
        grouped[cat] = [];
      }
      grouped[cat].push(fav);
    });

    const orderMap = {};
    readList(storageKeys.categoryOrder).forEach((cat, index) => {
      orderMap[cat] = index;
    });
    const categoriesList = Array.from(allCategories).sort((a, b) => {
      const idxA = orderMap[a] !== undefined ? orderMap[a] : 999999;
      const idxB = orderMap[b] !== undefined ? orderMap[b] : 999999;
      if (idxA !== idxB) {
        return idxA - idxB;
      }
      if (a === "기본") {
        return -1;
      }
      if (b === "기본") {
        return 1;
      }
      return a.localeCompare(b);
    });
    const collapsedCategories = new Set(readList(storageKeys.collapsedCategories));

    categoriesList.forEach((cat) => {
      const items = grouped[cat];
      if ((!items || items.length === 0) && !customCategories.includes(cat)) {
        return;
      }

      const isCollapsed = collapsedCategories.has(cat);
      const groupDiv = document.createElement("div");
      groupDiv.className = "favorites-category";
      groupDiv.dataset.category = cat;
      if (isCollapsed) {
        groupDiv.classList.add("is-collapsed");
      }

      const headerDiv = document.createElement("div");
      headerDiv.className = "category-header";
      headerDiv.draggable = true;
      headerDiv.dataset.category = cat;
      const toggleSpan = document.createElement("span");
      toggleSpan.className = "category-toggle-icon";
      toggleSpan.textContent = isCollapsed ? "▸" : "▾";
      const nameSpan = document.createElement("span");
      nameSpan.textContent = cat;
      headerDiv.append(toggleSpan, nameSpan);
      groupDiv.append(headerDiv);
      headerDiv.addEventListener("click", () => {
        if (collapsedCategories.has(cat)) {
          collapsedCategories.delete(cat);
        } else {
          collapsedCategories.add(cat);
        }
        writeJson(storageKeys.collapsedCategories, Array.from(collapsedCategories));
        render();
      });
      headerDiv.addEventListener("dragstart", (event) => {
        event.dataTransfer.setData("text/plain", JSON.stringify({ type: "category", category: cat }));
        event.dataTransfer.effectAllowed = "move";
      });
      bindDrop(headerDiv, "category-drag-over", (event) => {
        try {
          const data = JSON.parse(event.dataTransfer.getData("text/plain") || "{}");
          if (data.type !== "category" || data.category === cat) {
            return;
          }
          const currentOrder = Array.from(categoriesList);
          const fromIdx = currentOrder.indexOf(data.category);
          const toIdx = currentOrder.indexOf(cat);
          if (fromIdx > -1 && toIdx > -1) {
            currentOrder.splice(fromIdx, 1);
            currentOrder.splice(toIdx, 0, data.category);
            writeJson(storageKeys.categoryOrder, currentOrder);
            render();
          }
        } catch {
          // 잘못된 드래그 데이터는 순서를 바꾸지 않는다.
        }
      });
      bindDrop(groupDiv, "drag-over", (event) => {
        try {
          const data = JSON.parse(event.dataTransfer.getData("text/plain") || "{}");
          if (data.type === "streamer" && data.sourceCategory !== cat) {
            void updateCategory(data.platform, data.channelId, cat);
          }
        } catch {
          // 잘못된 드래그 데이터는 카테고리를 바꾸지 않는다.
        }
      });

      const itemsDiv = document.createElement("div");
      itemsDiv.className = "category-items";
      if (items && items.length > 0) {
        items.sort((a, b) => {
          const aLive = a.status === "live" ? 1 : 0;
          const bLive = b.status === "live" ? 1 : 0;
          if (aLive !== bLive) {
            return bLive - aLive;
          }
          if (a.status === "live" && (a.viewerCount || 0) !== (b.viewerCount || 0)) {
            return (b.viewerCount || 0) - (a.viewerCount || 0);
          }
          return a.streamerName.localeCompare(b.streamerName);
        });
        items.forEach((fav) => {
          const item = document.createElement("button");
          item.className = `favorite-item is-${fav.status}`;
          item.draggable = true;
          item.dataset.platform = fav.platform;
          item.dataset.channelId = fav.channelId;
          item.type = "button";
          if (fav.status === "live") {
            item.addEventListener("click", () => {
              item.dispatchEvent(new CustomEvent("raider:feature", {
                bubbles: true,
                detail: { platform: fav.platform, channelId: fav.channelId },
              }));
            });
          }
          item.addEventListener("dragstart", (event) => {
            event.dataTransfer.setData("text/plain", JSON.stringify({
              type: "streamer",
              platform: fav.platform,
              channelId: fav.channelId,
              sourceCategory: cat,
            }));
            event.dataTransfer.effectAllowed = "move";
            item.classList.add("is-dragging");
          });
          item.addEventListener("dragend", () => {
            item.classList.remove("is-dragging");
          });

          const dot = document.createElement("span");
          dot.className = "favorite-state-dot";
          dot.setAttribute("aria-hidden", "true");
          const copy = document.createElement("span");
          copy.className = "favorite-item-copy";
          const name = document.createElement("strong");
          name.textContent = fav.streamerName;
          const state = document.createElement("span");
          state.textContent = fav.status === "live" ? "라이브" : fav.status === "delayed" ? "상태 확인 지연" : "오프라인";
          copy.append(name, state);
          item.append(dot, copy);
          if (fav.status === "live" && fav.viewerCount !== undefined && fav.viewerCount !== null) {
            const viewerSpan = document.createElement("span");
            viewerSpan.className = "favorite-viewer";
            viewerSpan.textContent = fav.viewerCount.toLocaleString();
            item.append(viewerSpan);
          }
          itemsDiv.append(item);
        });
      } else {
        const emptyPlaceholder = document.createElement("div");
        emptyPlaceholder.className = "favorite-item-empty-placeholder";
        emptyPlaceholder.textContent = "여기에 스트리머를 드래그하세요.";
        itemsDiv.append(emptyPlaceholder);
      }

      groupDiv.append(itemsDiv);
      list.append(groupDiv);
    });
  }

  async function finishWrite() {
    if (afterWrite) {
      await afterWrite();
      return;
    }
    await load();
  }

  async function updateCategory(platform, channelId, newCategory) {
    try {
      await setFavoriteCategory(token, platform, channelId, newCategory);
      await finishWrite();
    } catch {
      status.textContent = "카테고리를 변경하지 못했습니다.";
    }
  }

  function bindStreamToggles() {
    document.querySelectorAll(".favorite-toggle").forEach((button) => {
      if (button.dataset.favoriteBound === "true") {
        return;
      }
      button.dataset.favoriteBound = "true";
      button.addEventListener("click", () => {
        void toggle(button);
      });
    });
  }

  async function load() {
    try {
      favorites.clear();
      (await listFavorites()).forEach((favorite) => {
        favorites.set(favoriteKey(favorite.platform, favorite.channelId), favorite);
      });
      bindStreamToggles();
      render();
    } catch {
      status.textContent = "즐겨찾기를 불러오지 못했습니다.";
    }
    if (onLoaded) {
      onLoaded();
    }
  }

  async function toggle(button) {
    const card = button.closest(".stream-card");
    const key = favoriteKey(card.dataset.platform, card.dataset.channelId);
    const removing = favorites.has(key);
    button.disabled = true;
    try {
      await toggleFavorite(token, card.dataset.platform, card.dataset.channelId, removing);
      await finishWrite();
    } catch {
      status.textContent = "즐겨찾기를 변경하지 못했습니다.";
    } finally {
      button.disabled = false;
    }
  }

  const addCategoryBtn = document.querySelector(".favorites-add-category");
  if (addCategoryBtn) {
    addCategoryBtn.addEventListener("click", (event) => {
      event.stopPropagation();
      const entered = prompt("새 카테고리 이름을 입력하세요:");
      if (!entered) {
        return;
      }
      const newCat = entered.trim();
      if (!newCat || newCat.length > 100) {
        return;
      }
      const customCategories = readList(storageKeys.customCategories);
      if (!customCategories.includes(newCat)) {
        customCategories.push(newCat);
        writeJson(storageKeys.customCategories, customCategories);
        render();
      }
    });
  }

  bindStreamToggles();
  void load();
  return {
    refresh: load,
    setAfterWrite(callback) {
      afterWrite = callback;
    },
    setOnLoaded(callback) {
      onLoaded = callback;
    },
  };
}
