// 고른 즐겨찾기 라이브 10장 안에서 큰 카드와 작은 카드 넷을 돌린다.
export function startFeatured() {
  let selectedKey = "";

  function slides() {
    return [...document.querySelectorAll("[data-featured-slide]")];
  }

  function show(index) {
    const nodes = slides();
    if (nodes.length === 0) {
      return;
    }

    const next = ((index % nodes.length) + nodes.length) % nodes.length;
    const slide = nodes[next];
    const mosaic = nodes.length >= 5;
    selectedKey = `${slide.dataset.platform}:${slide.dataset.channelId}`;
    nodes.forEach((node, nodeIndex) => {
      const offset = (nodeIndex - next + nodes.length) % nodes.length;
      const front = offset === 0;
      node.classList.toggle("is-front", front);
      if (mosaic && offset < 5) {
        node.dataset.slot = String(offset);
      } else {
        delete node.dataset.slot;
      }

      const face = node.querySelector(".featured-face");
      if (!face) {
        return;
      }

      if (front) {
        face.setAttribute("aria-current", "true");
      } else {
        face.removeAttribute("aria-current");
      }
    });

    const count = document.querySelector("[data-featured-count]");
    if (count) {
      count.textContent = `${next + 1} / ${nodes.length}`;
    }

    document.querySelectorAll(".favorite-item.is-picked").forEach((row) => {
      row.classList.remove("is-picked");
      row.removeAttribute("aria-current");
    });
    const platform = CSS.escape(slide.dataset.platform ?? "");
    const channelId = CSS.escape(slide.dataset.channelId ?? "");
    const picked = document.querySelector(`.favorite-item[data-platform="${platform}"][data-channel-id="${channelId}"]`);
    if (picked) {
      picked.classList.add("is-picked");
      picked.setAttribute("aria-current", "true");
    }
  }

  function restore() {
    const nodes = slides();
    if (nodes.length === 0) {
      return;
    }

    const index = nodes.findIndex((node) => `${node.dataset.platform}:${node.dataset.channelId}` === selectedKey);
    show(index >= 0 ? index : 0);
  }

  function showDrawn() {
    selectedKey = "";
    show(0);
  }

  document.addEventListener("click", (event) => {
    const step = event.target.closest("[data-step]");
    if (!step) {
      return;
    }

    const nodes = slides();
    const current = nodes.findIndex((node) => `${node.dataset.platform}:${node.dataset.channelId}` === selectedKey);
    show((current < 0 ? 0 : current) + Number(step.dataset.step));
  });

  document.addEventListener("raider:feature", (event) => {
    const detail = event.detail ?? {};
    const key = `${detail.platform}:${detail.channelId}`;
    const index = slides().findIndex((node) => `${node.dataset.platform}:${node.dataset.channelId}` === key);
    if (index >= 0) {
      show(index);
    }
  });

  restore();
  return { restore, showDrawn };
}
