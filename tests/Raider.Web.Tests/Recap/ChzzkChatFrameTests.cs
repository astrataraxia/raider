// 치지직 채팅 프레임에서 보낸 사람과 시각만 읽고 본문은 남기지 않는 계약을 검증한다.
using Raider.Web.Recap;

namespace Raider.Web.Tests.Recap;

public sealed class ChzzkChatFrameTests
{
    [Fact]
    public void ChatCommandYieldsSenderAndTimeWithoutMessageText()
    {
        var json =
            """
            {"cmd":93101,"bdy":[{"uid":"sender-1","msg":"secret text","msgTime":1710000000000,"profile":"{\"userIdHash\":\"sender-1\",\"nickname\":\"N\"}"}]}
            """;

        var chats = ChzzkChatFrame.Read(json);

        var chat = Assert.Single(chats);
        Assert.Equal("sender-1", chat.SenderChannelId);
        Assert.Equal(1710000000000, chat.MessageTimeMs);
        Assert.DoesNotContain("secret text", chat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DoubleEncodedProfileStillFindsSender()
    {
        var json =
            """
            {"cmd":93101,"bdy":[{"uid":"","msg":"hi","msgTime":1,"profile":"{\"userIdHash\":\"from-profile\"}"}]}
            """;

        var chat = Assert.Single(ChzzkChatFrame.Read(json));

        Assert.Equal("from-profile", chat.SenderChannelId);
    }

    [Fact]
    public void NonChatCommandsAreIgnored()
    {
        Assert.Empty(ChzzkChatFrame.Read("""{"cmd":10000,"bdy":{}}"""));
        Assert.Empty(ChzzkChatFrame.Read("""{"cmd":93102,"bdy":[{"uid":"donor","msg":"thanks","msgTime":1}]}"""));
    }

    [Fact]
    public void AnonymousUidWithoutProfileIsDropped()
    {
        Assert.Empty(ChzzkChatFrame.Read("""{"cmd":93101,"bdy":[{"uid":"anonymous","msg":"x","msgTime":1}]}"""));
    }
}
