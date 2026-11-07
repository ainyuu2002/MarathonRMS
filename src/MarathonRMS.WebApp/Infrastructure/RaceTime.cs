namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>Hiển thị thành tích: thời gian lưu bằng mili giây, công bố làm tròn lên giây (BR-22).</summary>
public static class RaceTime
{
    /// <summary>3725001 ⇒ "1:02:06".</summary>
    public static string Format(long? milliseconds)
    {
        if (milliseconds is not long ms) return "—";
        var seconds = (ms + 999) / 1000;
        return $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}";
    }

    public static string DnfReasonLabel(string? reason) => reason switch
    {
        "CutOff" => "Quá giờ giới hạn",
        "Voluntary" => "Tự bỏ cuộc",
        "Medical" => "Rút vì lý do y tế",
        "MissedCheckpoint" => "Bỏ qua checkpoint bắt buộc",
        _ => reason ?? ""
    };

    public static string GenderLabel(string gender) => gender == "F" ? "Nữ" : "Nam";
}
