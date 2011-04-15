/*************************************************************************
 *
 *   file		: TimerWheel.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2008-01-31 19:35:36 +0800 (Thu, 31 Jan 2008) $
 *   last author	: $LastChangedBy: tobz $
 *   revision		: $Rev: 87 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading;

namespace WoWExtractor
{
    public class TimerWheel : IDisposable
    {
        public const int DefaultSectorCount = 512;
        public const int DefaultSectorSpan = 1;
        public const int MaxCurrentCount = 0xFF;
        public const int MaxSectors = 0xFFFF;
        public const int MaxSpanSeconds = 0xFFFF;

        private int m_currentSectorIndex;
        private bool m_disposed;
        private int m_sectorCount;
        private List<TimerItem>[] m_sectors;
        private int m_sectorSpan;
        private System.Timers.Timer m_timer;

        public TimerWheel()
            : this(512, TimeSpan.FromMilliseconds(100))
        {
        }

        public TimerWheel(TimeSpan sectorSpan)
            : this(512, sectorSpan)
        {
        }

        public TimerWheel(int sectorCount, TimeSpan sectorSpan)
        {
            if ((sectorCount <= 0) || (sectorCount >= MaxSectors))
                throw new ArgumentOutOfRangeException("sectorCount");

            int totalSeconds = TimerItem.TotalSeconds(sectorSpan);
            if (totalSeconds < 1)
                throw new ArgumentOutOfRangeException("sectorSpan");

            m_sectorCount = sectorCount;
            m_sectorSpan = totalSeconds;
            m_sectors = new List<TimerItem>[m_sectorCount];
            for (int i = 0; i < m_sectorCount; i++)
            {
                m_sectors[i] = new List<TimerItem>();
            }
            m_timer = new System.Timers.Timer((double)(m_sectorSpan * 1000));
            m_timer.AutoReset = true;
            m_timer.Elapsed += Timer_Elapsed;
            m_timer.Start();
        }

        public void Slice()
        {
            int index;
            lock (m_sectors)
            {
                index = m_currentSectorIndex;
                m_currentSectorIndex = (m_currentSectorIndex + 1) % m_sectorCount;
            }

            List<TimerItem> timersToRemove = null;
            int count = 0;
            lock (m_sectors[index])
            {
                int listCount = m_sectors[index].Count;
                int subIndex = listCount - 1;

                for (int i = subIndex; i >= 0; i--)
                {
                    // handles moving timers down the list
                    TimerItem currentTimer = m_sectors[index][i];
                    if (currentTimer.DecrementCurrentCount() == 0)
                    {
                        currentTimer.SectorIndex = 0xFFFF;
                        m_sectors[index][i] = m_sectors[index][subIndex];
                        m_sectors[index][i].SectorListIndex = i;
                        m_sectors[index][subIndex--] = currentTimer;
                    }
                }

                count = (listCount - subIndex) - 1;
                if (count > 0)
                {
                    timersToRemove = m_sectors[index].GetRange(subIndex + 1, count);

                    m_sectors[index].RemoveRange(subIndex + 1, count);
                }
            }
            if (timersToRemove != null)
            {
                foreach (TimerItem timerItem in timersToRemove)
                {
                    ThreadPool.QueueUserWorkItem(timerItem.OnExpired, null);
                }
            }
            Thread.Sleep(1);
        }

        public void Add(TimerItem timerItem)
        {
            if (timerItem == null)
                throw new ArgumentNullException("timerItem");
            if (timerItem.Wheel != this)
                throw new InvalidOperationException("Invalid Timer Addition");

            if (!timerItem.IsStarted)
            {
                int currentSectorIndex = m_currentSectorIndex;
                int num2 = timerItem.SpanSeconds / m_sectorSpan;
                timerItem.CurrentCount = (num2 / m_sectorCount) + 1;
                int index = (num2 + currentSectorIndex) % m_sectorCount;

                if (index == currentSectorIndex)
                {
                    index = (index + 1) % m_sectorCount;
                }

                timerItem.SectorIndex = index;

                lock (m_sectors[index])
                {
                    m_sectors[index].Add(timerItem);
                    timerItem.SectorListIndex = m_sectors[index].Count - 1;
                }
            }
        }

        protected void CheckDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                m_timer.Elapsed -= Timer_Elapsed;
                m_timer.Dispose();
                m_disposed = true;
            }
        }

        ~TimerWheel()
        {
            Dispose(true);
        }

        public TimeSpan GetRemainingTimeToExpire(TimerItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            if (item.Wheel != this)
                throw new ArgumentException("Invalid Timer Item");

            lock (m_sectors)
            {
                int num = (item.CurrentCount - 1) * m_sectorCount;
                if (item.SectorIndex < m_currentSectorIndex)
                {
                    num += m_currentSectorIndex - item.SectorIndex;
                }
                else
                {
                    num += m_sectorCount - (m_currentSectorIndex - item.SectorIndex);
                }
                return new TimeSpan(0, 0, num * m_sectorSpan);
            }
        }

        public bool Remove(TimerItem timerItem)
        {
            if (timerItem == null)
                throw new ArgumentNullException("timerItem");

            if (timerItem.Wheel != this)
                throw new InvalidOperationException("Invalid Timer Removal");

            if (timerItem.IsStarted)
            {
                int index = timerItem.SectorIndex;
                if ((index < 0) || (index > m_sectors.Length))
                {
                    return false;
                }
                lock (m_sectors[index])
                {
                    int sectorListIndex = timerItem.SectorListIndex;
                    if ((sectorListIndex < m_sectors[index].Count) && (m_sectors[index][sectorListIndex] == timerItem))
                    {
                        if (sectorListIndex != (m_sectors[index].Count - 1))
                        {
                            TimerItem item = m_sectors[index][m_sectors[index].Count - 1];
                            m_sectors[index][sectorListIndex] = item;
                            item.SectorListIndex = sectorListIndex;
                        }

                        m_sectors[index].RemoveAt(m_sectors[index].Count - 1);
                        timerItem.SectorIndex = 0xFFFF;

                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            int index;
            lock (m_sectors)
            {
                index = m_currentSectorIndex;
                m_currentSectorIndex = (m_currentSectorIndex + 1) % m_sectorCount;
            }

            List<TimerItem> timersToRemove = null;
            int count = 0;
            lock (m_sectors[index])
            {
                int listCount = m_sectors[index].Count;
                int subIndex = listCount - 1;

                for (int i = subIndex; i >= 0; i--)
                {
                    // handles moving timers down the list
                    TimerItem currentTimer = m_sectors[index][i];
                    if (currentTimer.DecrementCurrentCount() == 0)
                    {
                        currentTimer.SectorIndex = 0xFFFF;
                        TimerItem item2 = m_sectors[index][subIndex];
                        m_sectors[index][i] = item2;
                        item2.SectorListIndex = i;
                        m_sectors[index][subIndex--] = currentTimer;
                    }
                }

                count = (listCount - subIndex) - 1;
                if (count > 0)
                {
                    timersToRemove = m_sectors[index].GetRange(subIndex + 1, count);

                    m_sectors[index].RemoveRange(subIndex + 1, count);
                }
            }
            if (timersToRemove != null)
            {
                foreach (TimerItem timerItem in timersToRemove)
                {
                    ThreadPool.QueueUserWorkItem(timerItem.OnExpired, null);
                }
            }
        }
    }
}
