namespace FA.Items
{
    /// <summary>
    /// (Hợp nhất item) Trạng thái MIỄN THỜI TIẾT có thời hạn — chỉ là MARKER + tint/nháy (base lo). Hệ
    /// hazard/biome (CHẶNG 2) kiểm tra bằng GetComponent&lt;WeatherImmunityStatus&gt;()?.IsActive để bỏ qua
    /// sát thương thời tiết. Kế thừa <see cref="TimedCharacterStatus"/>; dedup theo TYPE (1 component/nhân vật).
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public sealed class WeatherImmunityStatus : TimedCharacterStatus
    {
    }
}
