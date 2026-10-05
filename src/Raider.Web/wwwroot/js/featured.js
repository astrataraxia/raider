// 즐겨찾기 라이브 피처드 카드를 클릭으로만 넘긴다.
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
    selectedKey = `${slide.dataset.platform}:${slide.dataset.channelId}`;
    paint(slide, next, nodes);
  }

  function restore() {
    const nodes = slides();
    if (nodes.length === 0) {
      return;
    }

    const index = nodes.findIndex((node) => `${node.dataset.platform}:${node.dataset.channelId}` === selectedKey);
    show(index >= 0 ? index : 0);
  }

  function paint(slide, index, nodes) {
    const root = document.querySelector("[data-featured]");
    if (!root) {
      return;
    }

    const name = slide.dataset.name ?? "";
    const title = slide.dataset.title ?? "";
    const viewers = Number(slide.dataset.viewers ?? "0");
    const thumb = slide.dataset.thumb ?? "";
    const tags = (slide.dataset.tags ?? "").split("\u001f").filter((tag) => tag.length > 0);
    const nameNode = root.querySelector("[data-featured-name]");
    if (nameNode) {
      nameNode.textContent = name;
    }
    const titleNode = root.querySelector("[data-featured-title]");
    if (titleNode) {
      titleNode.textContent = title;
    }
    const viewersNode = root.querySelector("[data-featured-viewers]");
    if (viewersNode) {
      viewersNode.textContent = viewers.toLocaleString("ko-KR");
    }
    const lineNode = root.querySelector("[data-featured-line]");
    if (lineNode) {
      lineNode.textContent = `${slide.dataset.platformLabel ?? ""} · ${slide.dataset.line ?? ""}`;
    }
    const watchNode = root.querySelector("[data-featured-watch]");
    if (watchNode) {
      watchNode.href = slide.dataset.watch ?? "";
    }

    const photo = root.querySelector(".featured-photo");
    const fallback = root.querySelector("[data-featured-fallback]");
    if (photo) {
      if (thumb) {
        photo.hidden = false;
        photo.alt = `${name}의 방송 썸네일`;
        if (photo.getAttribute("src") !== thumb) {
          photo.setAttribute("src", thumb);
        }
      } else {
        photo.hidden = true;
        photo.removeAttribute("src");
      }
    }
    if (fallback) {
      fallback.hidden = thumb.length > 0;
    }

    const tagsNode = root.querySelector("[data-featured-tags]");
    if (tagsNode) {
      tagsNode.replaceChildren();
      tags.slice(0, 3).forEach((tag) => {
        const span = document.createElement("span");
        span.textContent = tag;
        tagsNode.append(span);
      });
    }

    const count = root.querySelector("[data-featured-count]");
    if (count) {
      count.textContent = `${index + 1} / ${nodes.length}`;
    }

    const multiple = nodes.length >= 2;
    root.querySelectorAll("[data-featured-controls], [data-featured-dots], [data-other-row], .media-arrow").forEach((node) => {
      node.hidden = !multiple;
    });

    const dots = root.querySelector("[data-featured-dots]");
    if (dots) {
      dots.replaceChildren();
      if (multiple) {
        nodes.forEach((_, dotIndex) => {
          const button = document.createElement("button");
          button.type = "button";
          button.dataset.go = String(dotIndex);
          button.setAttribute("aria-label", `${dotIndex + 1}번째 즐겨찾기`);
          if (dotIndex === index) {
            button.setAttribute("aria-current", "true");
          }
          dots.append(button);
        });
      }
    }

    const otherList = root.querySelector("[data-other-list]");
    if (otherList) {
      otherList.replaceChildren();
      if (multiple) {
        for (let offset = 1; offset < nodes.length && offset <= 3; offset += 1) {
          const other = nodes[(index + offset) % nodes.length];
          const button = document.createElement("button");
          button.type = "button";
          button.dataset.other = "";
          button.dataset.platform = other.dataset.platform ?? "";
          button.dataset.channelId = other.dataset.channelId ?? "";
          const copy = document.createElement("span");
          copy.className = "other-copy";
          const strong = document.createElement("strong");
          strong.textContent = other.dataset.name ?? "";
          const meta = document.createElement("span");
          meta.textContent = other.dataset.line ?? "";
          copy.append(strong, meta);
          const viewersLabel = document.createElement("span");
          viewersLabel.className = "other-count";
          viewersLabel.textContent = Number(other.dataset.viewers ?? "0").toLocaleString("ko-KR");
          button.append(copy, viewersLabel);
          otherList.append(button);
        }
      }
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

  document.addEventListener("click", (event) => {
    const step = event.target.closest("[data-step]");
    const dot = event.target.closest("[data-go]");
    const other = event.target.closest("[data-other]");
    if (step) {
      const nodes = slides();
      const current = nodes.findIndex((node) => `${node.dataset.platform}:${node.dataset.channelId}` === selectedKey);
      show((current < 0 ? 0 : current) + Number(step.dataset.step));
      return;
    }
    if (dot) {
      show(Number(dot.dataset.go));
      return;
    }
    if (other) {
      const key = `${other.dataset.platform}:${other.dataset.channelId}`;
      const index = slides().findIndex((node) => `${node.dataset.platform}:${node.dataset.channelId}` === key);
      if (index >= 0) {
        show(index);
      }
    }
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
  return { restore };
}
