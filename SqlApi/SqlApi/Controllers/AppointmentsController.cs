using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SqlApi.DTOs;

namespace SqlApi.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IConfiguration _config;
    public AppointmentsController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments(string? status, string? patientLastName)
    {
        string DefaultConnection =  _config.GetConnectionString("DefaultConnection");
        
        await using var conn = new SqlConnection(DefaultConnection);
        
        await conn.OpenAsync();
        
        await using var command = new SqlCommand("""
                                                 SELECT
                                                     a.IdAppointment,
                                                     a.AppointmentDate,
                                                     a.Status,
                                                     a.Reason,
                                                     p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                     p.Email AS PatientEmail
                                                 FROM dbo.Appointments a
                                                 JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                                 WHERE (@Status IS NULL OR a.Status = @Status)
                                                   AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                                                 ORDER BY a.AppointmentDate;
                                                 """, conn);

        command.Parameters.Add("@Status", SqlDbType.VarChar).Value = status ?? (object) DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.VarChar).Value = patientLastName ?? (object) DBNull.Value;

        var result = await command.ExecuteReaderAsync();

        var appointments = new List<AppointmentListDto>();

        while (await result.ReadAsync())
        {
            var dto = new AppointmentListDto
            {
                IdAppointment = (int)result["IdAppointment"],
                AppointmentDate = (DateTime) result["AppointmentDate"],
                Status = (string) result["Status"],
                Reason = (string) result["Reason"],
                PatientFullName = (string) result["PatientFullName"],
                PatientEmail = (string) result["PatientEmail"]
            };
            appointments.Add(dto);
        }
        
        return Ok(appointments);
    }

    [HttpGet("{idAppointment}")]
    public async Task<IActionResult> GetAppointment(int idAppointment)
    {
        string DefaultConnection = _config.GetConnectionString("DefaultConnection");

        await using var conn = new SqlConnection(DefaultConnection);

        await conn.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT
                                                     a.IdAppointment,
                                                     a.AppointmentDate,
                                                     a.Status,
                                                     a.Reason,
                                                     p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                     p.Email AS PatientEmail,
                                                     p.PhoneNumber as PatientPhone,
                                                     d.LicenseNumber as DoctorLicenseNumber,
                                                     a.InternalNotes,
                                                     a.CreatedAt as AppointmentCreation
                                                 FROM dbo.Appointments a
                                                 JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                                 JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                                                 WHERE (@IdAppointment IS NULL OR a.IdAppointment = @IdAppointment)
                                                 ORDER BY a.AppointmentDate;
                                                 """, conn);
        
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        var result = await command.ExecuteReaderAsync();

        if (await result.ReadAsync() == false)
        {
            return NotFound();
        }

        var appointment = new AppointmentDetailsDto
        {
            IdAppointment = (int)result["IdAppointment"],
            AppointmentDate = (DateTime) result["AppointmentDate"],
            Status = (string) result["Status"],
            Reason = (string) result["Reason"],
            PatientFullName = (string) result["PatientFullName"],
            PatientEmail = (string) result["PatientEmail"],
            PatientPhone = (string) result["PatientPhone"],
            DoctorLicenseNumber = (string) result["DoctorLicenseNumber"],
            InternalNotes = result["InternalNotes"]==DBNull.Value ? string.Empty : (string) result["InternalNotes"], 
            AppointmentCreation =  (DateTime) result["AppointmentCreation"]
        };
        
        return Ok(appointment);
    }
}