// 즐겨찾기와 태그 서랍이 같은 localStorage JSON 읽기·쓰기를 쓴다.
export const storageKeys = {
  customCategories: "raider.favorites.customCategories",
  categoryOrder: "raider.favorites.categoryOrder",
  collapsedCategories: "raider.favorites.collapsedCategories",
  sidebarCollapsed: "raider.favorites.sidebarCollapsed",
  tagDrawer: "tagDrawerOpen",
};

export function readJson(key, fallback) {
  try {
    const text = localStorage.getItem(key);
    if (!text) {
      return fallback;
    }

    return JSON.parse(text);
  } catch {
    return fallback;
  }
}

export function writeJson(key, value) {
  localStorage.setItem(key, JSON.stringify(value));
}

export function readFlag(key) {
  return localStorage.getItem(key) === "true";
}

export function writeFlag(key, value) {
  localStorage.setItem(key, value ? "true" : "false");
}
