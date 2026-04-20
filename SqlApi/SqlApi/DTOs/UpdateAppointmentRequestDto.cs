namespace SqlApi.DTOs;

public class UpdateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public required string Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
}