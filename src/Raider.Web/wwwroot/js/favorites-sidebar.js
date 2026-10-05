// 즐겨찾기 사이드바의 접기와 모바일 서랍을 다룬다.
import { readFlag, storageKeys, writeFlag } from "#storage";

export function bindSidebar(elements) {
  const { sidebar, collapse, mobileToggle, backdrop, pageShell, siteHeader } = elements;
  const mobileQuery = window.matchMedia("(max-width: 900px)");

  function setCollapsed(collapsed) {
    sidebar.classList.toggle("is-collapsed", collapsed);
    collapse.setAttribute("aria-expanded", collapsed ? "false" : "true");
    collapse.setAttribute("aria-label", collapsed ? "즐겨찾기 펼치기" : "즐겨찾기 접기");
    collapse.textContent = collapsed ? "›" : "‹";
    writeFlag(storageKeys.sidebarCollapsed, collapsed);
  }

  function setMobileOpen(open) {
    sidebar.classList.toggle("is-mobile-open", open);
    document.body.classList.toggle("favorites-drawer-open", open);
    mobileToggle.setAttribute("aria-expanded", open ? "true" : "false");
    mobileToggle.setAttribute("aria-label", open ? "즐겨찾기 닫기" : "즐겨찾기 열기");
    sidebar.inert = !open;
    pageShell.inert = open;
    siteHeader.inert = open;
    if (!open) {
      mobileToggle.focus();
    }
  }

  function syncViewport() {
    if (mobileQuery.matches) {
      sidebar.inert = !sidebar.classList.contains("is-mobile-open");
      return;
    }

    sidebar.classList.remove("is-mobile-open");
    document.body.classList.remove("favorites-drawer-open");
    mobileToggle.setAttribute("aria-expanded", "false");
    mobileToggle.setAttribute("aria-label", "즐겨찾기 열기");
    sidebar.inert = false;
    pageShell.inert = false;
    siteHeader.inert = false;
  }

  collapse.addEventListener("click", () => {
    if (mobileQuery.matches) {
      setMobileOpen(false);
    } else {
      setCollapsed(!sidebar.classList.contains("is-collapsed"));
    }
  });
  mobileToggle.addEventListener("click", () => {
    setMobileOpen(!sidebar.classList.contains("is-mobile-open"));
  });
  backdrop.addEventListener("click", () => setMobileOpen(false));
  mobileQuery.addEventListener("change", syncViewport);
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && sidebar.classList.contains("is-mobile-open")) {
      setMobileOpen(false);
    }
  });

  setCollapsed(readFlag(storageKeys.sidebarCollapsed));
  syncViewport();
}
