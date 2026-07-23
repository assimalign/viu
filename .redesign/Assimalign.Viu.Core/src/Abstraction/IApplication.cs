namespace Assimalign.Viu;

/// <summary>Represents one configured, platform-neutral Viu application.</summary>
public interface IApplication
{
    /// <summary>Gets the immutable application composition context.</summary>
    IApplicationContext Context { get; }
}

