namespace QuanAnWinForm.Models;

public class SearchResultDto
{
    public string Type { get; set; } = ""; // "MenuItem","Table","Customer","Employee","Order","Reservation"
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
}