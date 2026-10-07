// 홈 화면의 즐겨찾기, 피처드, 태그 서랍, 부분 갱신을 시작한다.
import { startFavorites } from "#favorites-list";
import { startFeatured } from "#featured";
import { startRefresh } from "#refresh";
import { startTags } from "#tags";

const favorites = startFavorites();
const featured = startFeatured();
startTags();
const refreshHtml = startRefresh(async () => {
  featured.showDrawn();
  // 수집이 완료되었을 때만 즐겨찾기 갱신 (수집 중일 때는 기존 데이터 유지)
  const liveContent = document.querySelector("[data-live-content]");
  const isRefreshing = liveContent?.dataset.refreshing === "true";
  if (!isRefreshing) {
    await favorites.refresh();
  }
});
favorites.setAfterWrite(refreshHtml);
favorites.setOnLoaded(() => featured.restore());
