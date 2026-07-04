namespace EcaInformationSystem.Client.Services
{
    public static class ChatFileIconHelper
    {
        public static string GetIcon(string? contentType) => contentType switch
        {
            "application/pdf" => "bi-file-earmark-pdf-fill text-danger",
            "application/msword" or
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "bi-file-earmark-word-fill text-primary",
            "application/vnd.ms-excel" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "bi-file-earmark-excel-fill text-success",
            "application/vnd.ms-powerpoint" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => "bi-file-earmark-ppt-fill text-warning",
            "text/csv" => "bi-file-earmark-spreadsheet-fill text-success",
            "text/plain" => "bi-file-earmark-text-fill text-secondary",
            _ => "bi-file-earmark-fill text-muted"
        };
    }
}
