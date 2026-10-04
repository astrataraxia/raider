// SQLite 초기화가 끝났는지 상태 화면에 알린다.
namespace Raider.Web.Status;

public sealed class StoreGate
{
    private int ready;

    public bool IsReady => Volatile.Read(ref ready) == 1;

    public void MarkReady() => Volatile.Write(ref ready, 1);
}
