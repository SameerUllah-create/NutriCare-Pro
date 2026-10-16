namespace NutriCare.Domain.Exceptions;

/// <summary>An authenticated provider event which this integration does not subscribe to.</summary>
public sealed class UnsupportedProviderEventException() : Exception("This signed provider event is not applicable") { }
