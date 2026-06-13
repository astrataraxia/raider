// Raider 디자인 프로토타입의 필터와 검색 상호작용을 제공한다.
const filterButtons = [...document.querySelectorAll(".filter-button")];
const tagButtons = [...document.querySelectorAll(".tag-button")];
const searchInput = document.querySelector("#stream-search");
const streamCards = [...document.querySelectorAll(".stream-card")];
const streamGrid = document.querySelector("#stream-grid");
const emptyState = document.querySelector("#empty-state");
const visibleCount = document.querySelector("#visible-count");
const resetFilter = document.querySelector("#reset-filter");
const notice = document.querySelector(".notice-banner");

let selectedPlatform = "all";
let selectedTag = "all";

streamCards.forEach((card) => {
  const tags = card.dataset.tags.split(" ");
  const tagList = document.createElement("div");
  tagList.className = "card-tags";
  tagList.setAttribute("aria-label", "방송 태그");

  tags.slice(0, 3).forEach((tag) => {
    const label = document.createElement("span");
    label.textContent = tag;
    tagList.append(label);
  });

  card.querySelector(".card-copy").append(tagList);
});

function updateStreams() {
  const query = searchInput.value.trim().toLocaleLowerCase("ko");
  let count = 0;

  streamCards.forEach((card) => {
    const matchesPlatform = selectedPlatform === "all" || card.dataset.platform === selectedPlatform;
    const matchesTag = selectedTag === "all" || card.dataset.tags.split(" ").includes(selectedTag);
    const matchesQuery = !query || card.dataset.search.toLocaleLowerCase("ko").includes(query);
    const visible = matchesPlatform && matchesTag && matchesQuery;

    card.hidden = !visible;
    count += visible ? 1 : 0;
  });

  visibleCount.textContent = count;
  streamGrid.hidden = count === 0;
  emptyState.hidden = count !== 0;
}

filterButtons.forEach((button) => {
  button.addEventListener("click", () => {
    selectedPlatform = button.dataset.platform;

    filterButtons.forEach((candidate) => {
      const active = candidate === button;
      candidate.classList.toggle("is-active", active);
      candidate.setAttribute("aria-pressed", String(active));
    });

    updateStreams();
  });
});

tagButtons.forEach((button) => {
  button.addEventListener("click", () => {
    selectedTag = button.dataset.tag;

    tagButtons.forEach((candidate) => {
      const active = candidate === button;
      candidate.classList.toggle("is-active", active);
      candidate.setAttribute("aria-pressed", String(active));
    });

    updateStreams();
  });
});

searchInput.addEventListener("input", updateStreams);

document.addEventListener("keydown", (event) => {
  if (event.key === "/" && document.activeElement !== searchInput) {
    event.preventDefault();
    searchInput.focus();
  }
});

resetFilter.addEventListener("click", () => {
  selectedPlatform = "all";
  selectedTag = "all";
  searchInput.value = "";
  filterButtons[0].click();
  tagButtons[0].click();
});

document.querySelector("#dismiss-notice").addEventListener("click", () => {
  notice.hidden = true;
});
