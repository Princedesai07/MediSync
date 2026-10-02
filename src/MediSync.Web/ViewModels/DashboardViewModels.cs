namespace MediSync.Web.ViewModels;
public class DashboardViewModel
{
    public int TotalPatients { get; set; }
    public int TotalDoctors { get; set; }
    public int TodaysAppointments { get; set; }
    public int PendingAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int PendingLabTests { get; set; }
    public int CompletedLabTests { get; set; }
    public int TotalMedicines { get; set; }
    public int LowStockMedicines { get; set; }
    public int TotalMedicalRecords { get; set; }
    public int TotalPrescriptions { get; set; }
    public int PendingPrescriptions { get; set; }
    public int DispensedPrescriptions { get; set; }
    public int ProcessingPrescriptions { get; set; }
    public int InProgressLabTests { get; set; }
    public int ExpiredMedicines { get; set; }
    public int UpcomingAppointments { get; set; }
    public int MyAppointments { get; set; }
    public int MyMedicalRecords { get; set; }
    public int MyLabResults { get; set; }
    public int MyPrescriptions { get; set; }
    public string? NextAppointment { get; set; }
    public string? MyName { get; set; }
    public string? MyEmail { get; set; }
    public string? MyPhone { get; set; }
    public string? MyGender { get; set; }
    public DateTime? MyDateOfBirth { get; set; }
    public string? MyEmergencyContact { get; set; }
    public string? MyAddress { get; set; }
    public DateTime? MyRegistrationDate { get; set; }
}
