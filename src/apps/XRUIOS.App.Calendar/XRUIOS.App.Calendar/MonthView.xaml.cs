using System;
using System.Windows.Controls;
using XRUIOS.App.Calendar.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Calendar
{
    /// <summary>
    /// Month grid + side agenda. On load, connects to the Calendar worker
    /// through AppShell and fills the agenda list from CalendarPicker.
    /// The month grid itself is procedurally rendered by the MonthGrid
    /// control (a UserControl - open its .xaml to edit the cell template).
    /// </summary>
    public partial class MonthView : Page
    {
        public MonthView()
        {
            InitializeComponent();
            MonthLabel.Text = DateTime.Today.ToString("MMMM yyyy");

            // Seed sample rows so the pane looks live before the broker replies.
            for (var i = 0; i < 5; i++)
                Agenda.Items.Add(new AgendaCard("Online", "Calendar Event", "10:00AM"));

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Calendar");
                    var calendars = await CalendarPicker.ListAsync();
                    if (calendars.Count > 0)
                    {
                        Agenda.Items.Clear();
                        foreach (var cal in calendars)
                            Agenda.Items.Add(new AgendaCard("Calendar", cal.DisplayName, cal.Color));
                    }
                }
                catch
                {
                    // Broker unreachable - keep sample rows so the layout still reads.
                }
            };
        }
    }
}
