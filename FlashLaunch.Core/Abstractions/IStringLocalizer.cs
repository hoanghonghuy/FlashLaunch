namespace FlashLaunch.Core.Abstractions;

public interface IStringLocalizer
{
    string this[string key] { get; }

    string Format(string key, params object[] args);
}
