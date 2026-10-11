namespace Luckbound.Core.Input
{
    public interface IPlayerInputSource
    {
        PlayerCommand Consume(long tick);
    }
}
