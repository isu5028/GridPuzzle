using Cysharp.Threading.Tasks;

public interface IAsyncUndoCommand : ICommand
{
    UniTask UndoAsync();
}
