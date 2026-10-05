// 공용 즐겨찾기 API 호출과 카드 키를 한곳에 둔다.
export function readToken() {
  return document.querySelector("[data-antiforgery-token] input")?.value ?? "";
}

export function favoriteKey(platform, channelId) {
  return `${platform}:${channelId}`;
}

export async function listFavorites() {
  const response = await fetch("/api/favorites", { headers: { Accept: "application/json" } });
  if (!response.ok) {
    throw new Error("favorites unavailable");
  }

  return response.json();
}

export async function setFavoriteCategory(token, platform, channelId, category) {
  const response = await fetch(
    `/api/favorites/${encodeURIComponent(platform)}/${encodeURIComponent(channelId)}/category`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token,
      },
      body: JSON.stringify({ category }),
    },
  );
  if (!response.ok) {
    throw new Error("category update failed");
  }
}

export async function toggleFavorite(token, platform, channelId, removing) {
  const response = await fetch(
    `/api/favorites/${encodeURIComponent(platform)}/${encodeURIComponent(channelId)}`,
    {
      method: removing ? "DELETE" : "PUT",
      headers: { RequestVerificationToken: token },
    },
  );
  if (!response.ok) {
    throw new Error("favorite update failed");
  }
}
