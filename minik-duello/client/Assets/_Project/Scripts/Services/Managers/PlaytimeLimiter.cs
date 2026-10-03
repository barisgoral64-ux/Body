using System;
using MinikDuello.Services.Save;

namespace MinikDuello.Services.Managers
{
    public enum LimitState
    {
        Unlimited,
        Active,
        Exceeded
    }

    /// <summary>Ebeveynin günlük süre sınırı. Dolunca nazik bir mola ekranı gösterilir (korkutma yok).</summary>
    public sealed class PlaytimeLimiter
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const int KeepDays = 14;

        private readonly SaveManager save;
        private readonly IClock clock;

        public PlaytimeLimiter(SaveManager save, IClock clock)
        {
            this.save = save;
            this.clock = clock;
        }

        public double MinutesToday()
        {
            return save.Data.PlayMinutes.TryGetValue(Today(), out double m) ? m : 0d;
        }

        public void AddPlaySeconds(double seconds)
        {
            if (seconds <= 0) return;
            save.Update(d =>
            {
                string key = Today();
                d.PlayMinutes.TryGetValue(key, out double current);
                d.PlayMinutes[key] = current + seconds / 60d;
                Prune(d);
            });
        }

        public LimitState State(int dailyLimitMinutes)
        {
            if (dailyLimitMinutes <= 0) return LimitState.Unlimited;
            return MinutesToday() >= dailyLimitMinutes ? LimitState.Exceeded : LimitState.Active;
        }

        public double RemainingMinutes(int dailyLimitMinutes)
        {
            return dailyLimitMinutes <= 0 ? double.PositiveInfinity : Math.Max(0d, dailyLimitMinutes - MinutesToday());
        }

        private string Today() => clock.LocalNow.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);

        private void Prune(SaveData d)
        {
            if (d.PlayMinutes.Count <= KeepDays) return;
            var keys = new System.Collections.Generic.List<string>(d.PlayMinutes.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count - KeepDays; i++) d.PlayMinutes.Remove(keys[i]);
        }
    }
}
