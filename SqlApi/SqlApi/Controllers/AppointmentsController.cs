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

    [HttpPost]
    public async Task<IActionResult> CreateAppointment(CreateAppointmentRequestDto appointment)
    {
        if (appointment.AppointmentDate < DateTime.Now || String.IsNullOrEmpty(appointment.Reason) || appointment.Reason.Length > 250)
        {
            return BadRequest();
        }

        var DefaultConnection = _config.GetConnectionString("DefaultConnection");

        await using var conn = new SqlConnection(DefaultConnection);

        await conn.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT COUNT(1) FROM dbo.Doctors d
                                                 WHERE (@IdDoctor IS NULL OR d.IdDoctor = @IdDoctor)
                                                 AND IsActive = 1
                                                 """, conn);
        
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;

        var result = await command.ExecuteScalarAsync();

        if ((int)result == 0)
        {
            return BadRequest();
        }
        
        command.Parameters.Clear();
        command.CommandText = """
                              SELECT COUNT(1) FROM dbo.Appointments a
                              WHERE  (@AppointmentDate IS NULL OR a.AppointmentDate = @AppointmentDate)
                              AND (@IdDoctor IS NULL OR a.IdDoctor = @IdDoctor)
                              """;
        
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointment.AppointmentDate;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;

        result = await command.ExecuteScalarAsync();

        if ((int)result > 0)
        {
            return Conflict();
        }
        
        command.Parameters.Clear();
        command.CommandText = """
                              INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Reason, Status) 
                              OUTPUT INSERTED.IdAppointment
                              VALUES (@IdPatient, @IdDoctor, @AppointmentDate, @Reason, N'Scheduled');
                              """;

        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = appointment.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointment.AppointmentDate;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = appointment.Reason;

        result = await command.ExecuteScalarAsync();
        
        return CreatedAtAction(nameof(GetAppointment), new {idAppointment = (int) result}, appointment);
    }

    [HttpPut("{idAppointment}")]
    public async Task<IActionResult> UpdateAppointment(int idAppointment, UpdateAppointmentRequestDto appointment)
    {
        if (appointment.Status != "Scheduled" && appointment.Status != "Completed" && appointment.Status != "Cancelled")
        {
            return BadRequest();
        }
        
        var DefaultConnection = _config.GetConnectionString("DefaultConnection");

        await using var conn = new SqlConnection(DefaultConnection);

        await conn.OpenAsync();

        await using var command = new SqlCommand("""
                                                 SELECT COUNT(1) FROM dbo.Doctors d
                                                 WHERE (@IdDoctor IS NULL OR d.IdDoctor = @IdDoctor)
                                                 AND IsActive = 1
                                                 """, conn);
        
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;

        var result = await command.ExecuteScalarAsync();

        if ((int)result == 0)
        {
            return BadRequest();
        }
        
        command.Parameters.Clear();
        command.CommandText = """
                              SELECT COUNT(1) FROM dbo.Patients p
                              WHERE (@IdPatient IS NULL OR p.IdPatient = @IdPatient)
                              AND IsActive = 1
                              """;
        
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = appointment.IdPatient;

        result = await command.ExecuteScalarAsync();

        if ((int)result == 0)
        {
            return BadRequest();
        }
        
        command.Parameters.Clear();
        command.CommandText = """
                              SELECT * FROM dbo.Appointments a
                              WHERE (@IdAppointment IS NULL OR a.IdAppointment = @IdAppointment)
                              """;
        
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        
        var result2 = await command.ExecuteReaderAsync();

        if (!await result2.ReadAsync())
        {
            return NotFound();
        }

        if ( (string)result2["Status"] == "Completed")
        {
            appointment.AppointmentDate = (DateTime) result2["AppointmentDate"];
        }
        await result2.CloseAsync();
        
        command.Parameters.Clear();
        command.CommandText = """
                              SELECT Count(1) FROM dbo.Appointments a
                              WHERE (@IdDoctor IS NULL OR a.IdDoctor = @IdDoctor)
                              AND (@AppointmentDate IS NULL OR a.AppointmentDate = @AppointmentDate)
                              AND a.IdAppointment != @IdAppointment
                              """;
        
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointment.AppointmentDate;
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        result = await command.ExecuteScalarAsync();

        if ((int)result > 0)
        {
            return Conflict();
        }
        
        command.Parameters.Clear();
        command.CommandText = """
                              UPDATE dbo.Appointments
                              SET IdPatient = @IdPatient,
                                  IdDoctor = @IdDoctor,
                                  AppointmentDate = @AppointmentDate,
                                  Status = @Status,
                                  Reason = @Reason,
                                  InternalNotes = @InternalNotes
                              WHERE IdAppointment = @IdAppointment;
                              """;
        
        command.Parameters.Add("@IdPatient", SqlDbType.Int).Value = appointment.IdPatient;
        command.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = appointment.IdDoctor;
        command.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointment.AppointmentDate;
        command.Parameters.Add("@Status", SqlDbType.NVarChar).Value = appointment.Status;
        command.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = appointment.Reason;
        command.Parameters.Add("@InternalNotes", SqlDbType.NVarChar).Value = appointment.InternalNotes ?? (object)DBNull.Value;
        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        
        await command.ExecuteNonQueryAsync();
        
        return NoContent();
    }
}