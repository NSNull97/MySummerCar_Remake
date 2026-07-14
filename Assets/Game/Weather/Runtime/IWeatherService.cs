namespace MSC.Weather
{
    /// <summary>
    /// Supplies normalized precipitation state to presentation and gameplay consumers.
    /// </summary>
    public interface IWeatherService
    {
        float RainIntensity { get; }
    }
}
