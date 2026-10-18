using System;

namespace SleepUntil
{
    // Independent of Unity so calendar boundaries can be tested without running the game.
    public sealed class SleepSchedule
    {
        public const double Dawn = 0.25; // GK2's EnvironmentEngine day/night boundary, 06:00.
        public const int WeekLength = 6;
        public int? Weekday { get; }
        private int previousDay;
        private double previousTime;
        public bool Completed { get; private set; }

        public SleepSchedule(int day, double time, int? weekday)
        {
            Validate(day, time);
            if (weekday.HasValue && (weekday.Value < 1 || weekday.Value > WeekLength))
                throw new ArgumentOutOfRangeException(nameof(weekday));
            previousDay = day;
            previousTime = time;
            Weekday = weekday;
        }

        public bool Observe(int day, double time)
        {
            Validate(day, time);
            if (Completed) return false; // A wake is emitted once.
            if (day < previousDay) throw new InvalidOperationException("Calendar moved backwards; cancel this sleep.");
            bool crossedDawn = time >= Dawn && (day > previousDay || previousTime < Dawn);
            previousDay = day;
            previousTime = time;
            if (crossedDawn && (!Weekday.HasValue || DayOfWeek(day) == Weekday.Value))
            {
                Completed = true;
                return true;
            }
            return false;
        }

        public static int DayOfWeek(int day) => ((day - 1) % WeekLength) + 1;

        // Uses the game's next-day override for a truthful preview during scripted calendar jumps.
        public static double PreviewDays(int day, double time, int nextDayStep, int? weekday)
        {
            Validate(day, time);
            if (nextDayStep < 1 || nextDayStep > WeekLength) throw new ArgumentOutOfRangeException(nameof(nextDayStep));
            if (weekday.HasValue && (weekday.Value < 1 || weekday.Value > WeekLength)) throw new ArgumentOutOfRangeException(nameof(weekday));
            if (time < Dawn && (!weekday.HasValue || DayOfWeek(day) == weekday.Value)) return Dawn - time;
            int nextDay = day + nextDayStep;
            int extra = weekday.HasValue ? (weekday.Value - DayOfWeek(nextDay) + WeekLength) % WeekLength : 0;
            // Calendar may skip dates, but only one midnight elapses in the simulation.
            return 1 - time + extra + Dawn;
        }

        private static void Validate(int day, double time)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day));
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0 || time >= 1)
                throw new ArgumentOutOfRangeException(nameof(time));
        }
    }
}
