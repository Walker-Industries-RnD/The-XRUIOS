using Hangfire;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using static XRUIOS.Barebones.XRUIOS;
using Attachment = Ical.Net.DataTypes.Attachment;
using Calendar = Ical.Net.Calendar;
using YuukoProtocol;

namespace XRUIOS.Barebones
{
    public static class CalendarClass
    {
        //We keep our XRUIOS events simple with three kinds of things
        //To keep things simple, we will attach media here as byte[] (Is what i'd like to tell myself)

        //C
        public static async Task<string> CreateSimpleEvent(
            DateTime eventDate,
            string summary,
            string description,
            TimeZoneInfo timezone = null,
            int durationHours = 0,
            List<FileRecord> attachmentsList = null)
        {
            timezone ??= TimeZoneInfo.Local;

            // Create start/end in the correct timezone
            var start = new CalDateTime(eventDate, timezone.Id);
            var end = new CalDateTime(eventDate.AddHours(durationHours), timezone.Id);

            var uid = Guid.NewGuid().ToString();

            var calendarEvent = new CalendarEvent
            {
                Summary = summary,
                Description = description,
                Start = start,
                End = end,
                Uid = uid
            };

            // Attach files (only 1 image recommended but you can put other stuff long as it isn't big)
            if (attachmentsList != null && attachmentsList.Count > 0)
            {
                var firstAttachment = attachmentsList[0];
                var mediaPath = await Media.GetFile(DataPath, firstAttachment.UUID, firstAttachment.File);

                byte[] fileBytes;
                using (var fs = new FileStream(mediaPath.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileBytes = new byte[fs.Length];
                    await fs.ReadAsync(fileBytes, 0, fileBytes.Length);
                }


                var binaryAttachment = new Attachment(fileBytes);

                calendarEvent.Attachments = new List<Ical.Net.DataTypes.Attachment> { binaryAttachment };
            }

            // Create the calendar
            var calendar = new Ical.Net.Calendar();
            calendar.Events.Add(calendarEvent);

            if (timezone != null)
            {
                var vtz = VTimeZone.FromSystemTimeZone(timezone);
                calendar.AddTimeZone(vtz);
            }

            var serializer = new CalendarSerializer();
            var serializedCalendar = serializer.SerializeToString(calendar);

            var directoryPath = Path.Combine(DataPath, "Calendar");

            var filename = Guid.NewGuid().ToString() + ".ics"; //We do this to not worry about overlapping names

            var filePath = Path.Combine(directoryPath, filename);

            await File.WriteAllTextAsync(filePath, serializedCalendar);


            return uid;

        }

        public static async Task<string> CreateRecurringEvent(
            DateTime eventDate,
            string summary,
            string description,
            RecurrencePattern recurrencePattern,
            TimeZoneInfo timezone = null,
            int durationHours = 0,
            List<FileRecord> attachmentsList = null)
        {
            timezone ??= TimeZoneInfo.Local;

            // Start/end in the correct timezone
            var start = new CalDateTime(eventDate, timezone.Id);
            var end = new CalDateTime(eventDate.AddHours(durationHours), timezone.Id);

            var uid = Guid.NewGuid().ToString();


            var calendarEvent = new CalendarEvent
            {
                Summary = summary,
                Description = description,
                Start = start,
                End = end,
                RecurrenceRules = new List<RecurrencePattern> { recurrencePattern },
                Uid = uid

            };

            // Attach files (only 1 image recommended)
            if (attachmentsList != null && attachmentsList.Count > 0)
            {
                var firstAttachment = attachmentsList[0];
                var mediaPath = await Media.GetFile(DataPath, firstAttachment.UUID, firstAttachment.File);

                byte[] fileBytes;
                using (var fs = new FileStream(mediaPath.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileBytes = new byte[fs.Length];
                    await fs.ReadAsync(fileBytes, 0, fileBytes.Length);
                }


                var binaryAttachment = new Attachment(fileBytes);


                calendarEvent.Attachments = new List<Ical.Net.DataTypes.Attachment> { binaryAttachment };
            }

            // Create calendar
            var calendar = new Ical.Net.Calendar();
            calendar.Events.Add(calendarEvent);

            if (timezone != null)
            {
                var vtz = VTimeZone.FromSystemTimeZone(timezone);
                calendar.AddTimeZone(vtz);
            }

            var serializer = new CalendarSerializer();
            var serializedCalendar = serializer.SerializeToString(calendar);

            var directoryPath = Path.Combine(DataPath, "Calendar");

            var filename = Guid.NewGuid().ToString() + ".ics"; //We do this to not worry about overlapping names

            var filePath = Path.Combine(directoryPath, filename);

            await File.WriteAllTextAsync(filePath, serializedCalendar);

            return uid;

        }

        //R
        public static List<CalendarEvent> LoadAllEvents()
        {
            var directoryPath = Path.Combine(DataPath, "Calendar");

            if (!Directory.Exists(directoryPath))
                return new List<CalendarEvent>();

            var files = Directory.GetFiles(directoryPath, "*.ics");
            var allEvents = new List<CalendarEvent>();

            foreach (var file in files)
            {
                var calendar = Calendar.Load(File.ReadAllText(file));
                allEvents.AddRange(calendar.Events);
            }

            return allEvents;
        }

        // Get all events on a specific day (local time)
        public static List<CalendarEvent> GetEventsForDay(DateTime day)
        {
            var allEvents = CalendarClass.LoadAllEvents();
            var startOfDay = day.Date;
            var endOfDay = startOfDay.AddDays(1);

            return allEvents
                .Where(ev =>
                {
                    var evStart = ev.Start.Value.ToLocalTime();
                    var evEnd = ev.End.Value.ToLocalTime();

                    return evStart < endOfDay && evEnd > startOfDay;
                })
                .ToList();
        }

        // Get all events within a specific timespan (inclusive)
        public static List<CalendarEvent> GetEventsInRange(DateTime start, DateTime end)
        {
            if (start > end) throw new ArgumentException("Start cannot be after end.");

            var allEvents = CalendarClass.LoadAllEvents();

            return allEvents
                .Where(ev =>
                {
                    var evStart = ev.Start.Value.ToLocalTime();
                    var evEnd = ev.End.Value.ToLocalTime();

                    return evStart < end && evEnd > start;
                })
                .ToList();
        }


        public static CalendarEvent? GetEventByUid(string uid)
        {
            var directoryPath = Path.Combine(DataPath, "Calendar");
            if (!Directory.Exists(directoryPath)) return null;

            foreach (var file in Directory.GetFiles(directoryPath, "*.ics"))
            {
                var calendar = Calendar.Load(File.ReadAllText(file));
                var ev = calendar.Events.FirstOrDefault(e => e.Uid == uid);
                if (ev != null) return ev;
            }

            return null;
        }

        //Get events by date/range

        // U
        public static async Task<CalendarEvent?> UpdateEventByUid(
        string uid,
        Action<CalendarEvent> updateAction)
        {
            var directoryPath = Path.Combine(DataPath, "Calendar");

            if (!Directory.Exists(directoryPath))
                return null;

            foreach (var file in Directory.GetFiles(directoryPath, "*.ics"))
            {
                var calendar = Calendar.Load(File.ReadAllText(file));
                var calendarEvent = calendar.Events.FirstOrDefault(e => e.Uid == uid);

                if (calendarEvent == null)
                    continue;

                BackgroundJob.Delete($"calendar:{uid}:*");

                updateAction(calendarEvent);

                var serializer = new CalendarSerializer();
                await File.WriteAllTextAsync(file, serializer.SerializeToString(calendar));

                // 4️⃣ Return updated event (caller schedules)
                return calendarEvent;
            }

            return null;
        }

        // D
        public static void DeleteEventByUid(string uid)
        {
            BackgroundJob.Delete($"calendar:{uid}:*");

            var directoryPath = Path.Combine(DataPath, "Calendar");

            if (!Directory.Exists(directoryPath))
                return;

            var files = Directory.GetFiles(directoryPath, "*.ics");
            foreach (var file in files)
            {
                var calendar = Calendar.Load(File.ReadAllText(file));

                var matchingEvent = calendar.Events.FirstOrDefault(e => e.Uid == uid);
                if (matchingEvent != null)
                {
                    calendar.Events.Remove(matchingEvent);

                    if (calendar.Events.Count == 0)
                        File.Delete(file);
                    else
                        File.WriteAllTextAsync(
                            file,
                            new CalendarSerializer().SerializeToString(calendar)
                        );

                    break;
                }
            }
        }



        //TEMPORARY
        public static class CalendarNotifications
        {
            public static void Notify(string summary)
            {
                // Do whatever "Hey, it's time!" logic you have here
                Console.WriteLine($"Reminder: {summary}");
                // Could also push system notifications, toast, etc.
            }
        }

        public static void ScheduleUpcomingOccurrences(
            IEnumerable<Occurrence> upcomingOccurrences,
            TimeSpan lookaheadWindow)
        {
            var now = DateTime.Now;
            var cutoff = now + lookaheadWindow;

            foreach (var occ in upcomingOccurrences)
            {
                if (occ.Source is not CalendarEvent calendarEvent)
                    continue;

                var startLocal = occ.Period.StartTime
                    .ToTimeZone(TimeZoneInfo.Local.Id)
                    .Value;

                if (startLocal < now || startLocal > cutoff)
                    continue;

                var delay = startLocal - now;
                var jobId = $"calendar:{calendarEvent.Uid}:{startLocal:O}";

                if (delay <= TimeSpan.Zero)
                {
                    BackgroundJob.Enqueue(
                        jobId,
                        () => CalendarNotifications.Notify(calendarEvent.Summary)
                    );
                }
                else
                {
                    BackgroundJob.Schedule(
                        jobId,
                        () => CalendarNotifications.Notify(calendarEvent.Summary),
                        delay
                    );
                }
            }
        }






    }


}
