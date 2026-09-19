using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Vehistra.Client.ViewModels;
using Vehistra.Domain.Entities;

namespace Vehistra.ClientTests;

/// <summary>
/// Befehle, die eine Auswahl brauchen, müssen abgeblendet sein, solange nichts
/// ausgewählt ist. Vorher kehrten sie wortlos zurück - für den Anwender sah
/// das aus wie eine kaputte Schaltfläche, und genau so wurde es gemeldet.
///
/// Die Ansichtsmodelle brauchen im Betrieb ein gutes Dutzend Dienste. Hier geht
/// es nur um die erzeugten Befehle, deshalb entsteht die Instanz ohne
/// Konstruktor: die Befehle werden erst beim Zugriff angelegt und lesen nur das
/// Auswahlfeld.
/// </summary>
public class BefehlSperrenTests
{
    public static IEnumerable<object[]> Gesperrt =>
    [
        [typeof(UsersViewModel), "EditCommand"],
        [typeof(UsersViewModel), "ToggleActiveCommand"],
        [typeof(UsersViewModel), "ResetPasswordCommand"],
        [typeof(DriverViewModel), "EditCommand"],
        [typeof(DriverViewModel), "OpenFileCommand"],
        [typeof(RolesViewModel), "EditCommand"],
        [typeof(RolesViewModel), "DeleteCommand"],
        [typeof(WorkshopViewModel), "PrintReportCommand"],
        [typeof(WorkshopViewModel), "ChangeStatusCommand"],
        [typeof(AccidentViewModel), "PrintReportCommand"],
        [typeof(AccidentViewModel), "CloseAccidentCommand"],
        [typeof(DamageViewModel), "CloseDamageCommand"],
        [typeof(VehicleListViewModel), "EditCommand"],
        [typeof(VehicleListViewModel), "ChangeStatusCommand"],
        [typeof(VehicleListViewModel), "AddMileageCommand"],
        [typeof(MaintenanceViewModel), "EditRuleCommand"],
        [typeof(MaintenanceViewModel), "DeleteRuleCommand"],
        [typeof(MaintenanceViewModel), "RecordMaintenanceCommand"],
        [typeof(LicensePlateViewModel), "ReserveCommand"],
        [typeof(InspectionViewModel), "AddInspectionCommand"],
        [typeof(RetiredVehiclesViewModel), "UndoRetirementCommand"],
        [typeof(DocumentsViewModel), "ArchiveCommand"],
        [typeof(BackupViewModel), "VerifyCommand"],
        [typeof(SettingsViewModel), "EditCategoryCommand"],
        [typeof(SettingsViewModel), "DeleteCategoryCommand"],
        [typeof(SettingsViewModel), "EditDamageCategoryCommand"],
        [typeof(SettingsViewModel), "EditStatusCommand"],
        [typeof(SettingsViewModel), "DeleteStatusCommand"],
        [typeof(SettingsViewModel), "ToggleWorkshopCommand"]
    ];

    [Theory]
    [MemberData(nameof(Gesperrt))]
    public void Ohne_Auswahl_ist_der_Befehl_abgeblendet(Type ansichtsmodell, string befehl)
    {
        var instanz = RuntimeHelpers.GetUninitializedObject(ansichtsmodell);
        var kommando = Lies(instanz, befehl);

        kommando.CanExecute(null).ShouldBeFalse(
            $"{ansichtsmodell.Name}.{befehl} muss ohne Auswahl abgeblendet sein.");
    }

    [Fact]
    public void Mit_Auswahl_wird_der_Befehl_wieder_freigegeben()
    {
        var ansichtsmodell = RuntimeHelpers.GetUninitializedObject(typeof(UsersViewModel));
        var befehl = Lies(ansichtsmodell, "EditCommand");

        befehl.CanExecute(null).ShouldBeFalse();

        typeof(UsersViewModel)
            .GetProperty("SelectedUser", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(ansichtsmodell, new User { UserName = "testuser" });

        befehl.CanExecute(null).ShouldBeTrue("Mit Auswahl muss die Schaltfläche wieder anklickbar sein.");
    }

    [Fact]
    public void Befehle_ohne_Auswahlbezug_bleiben_anklickbar()
    {
        // Gegenprobe: "Neu" und "Aktualisieren" haengen an keiner Auswahl und
        // duerfen durch diesen Durchgang nicht abgeblendet worden sein.
        var ansichtsmodell = RuntimeHelpers.GetUninitializedObject(typeof(UsersViewModel));

        Lies(ansichtsmodell, "CreateCommand").CanExecute(null).ShouldBeTrue();
        Lies(ansichtsmodell, "RefreshCommand").CanExecute(null).ShouldBeTrue();
    }

    private static ICommand Lies(object ansichtsmodell, string befehl)
    {
        var eigenschaft = ansichtsmodell.GetType()
            .GetProperty(befehl, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"{ansichtsmodell.GetType().Name} hat keinen Befehl {befehl}.");

        return (ICommand)eigenschaft.GetValue(ansichtsmodell)!;
    }
}
