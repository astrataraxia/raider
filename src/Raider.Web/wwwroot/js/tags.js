// 홈 태그 서랍의 열림 상태를 유지한다.
import { readFlag, storageKeys, writeFlag } from "#storage";

export function startTags() {
  const toggleBtn = document.getElementById("tag-toggle-btn");
  const drawer = document.getElementById("tag-drawer");
  if (!toggleBtn || !drawer) {
    return;
  }

  const apply = (isOpen) => {
    drawer.style.display = isOpen ? "block" : "none";
    toggleBtn.setAttribute("aria-expanded", isOpen ? "true" : "false");
    toggleBtn.textContent = isOpen ? "태그 접기 -" : "태그 더 보기 +";
    toggleBtn.classList.toggle("is-open", isOpen);
    writeFlag(storageKeys.tagDrawer, isOpen);
  };

  apply(readFlag(storageKeys.tagDrawer));
  toggleBtn.addEventListener("click", () => {
    apply(toggleBtn.getAttribute("aria-expanded") !== "true");
  });
}
