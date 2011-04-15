/*************************************************************************
 *
 *   file		: TimerItem.cs
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

namespace WoWExtractor
{
    public class TimerItem
    {
        #region Constants
        private const ulong SectorListIndexMask = 0x0000000000FFFFFF;   
        private const ulong CurrentCountMask = 0x00000000FF000000;
        private const ulong SectorIndexMask = 0x0000FFFF00000000;
        private const ulong SpanSecondsMask = 0xFFFF000000000000;
        private const int SectorListIndexShift = 0;
        private const int CurrentCountShift = 24;
        private const int SectorIndexShift = 32;
        private const int SpanSecondsShift = 48;
        #endregion

        private ulong m_state;
        private TimerWheel m_timerWheel;
        public event EventHandler Expired;

        public TimerItem(TimerWheel timerWheel, TimeSpan timeSpan)
        {
            if (timerWheel == null)
                throw new ArgumentNullException("timerWheel");

            int totalSeconds = TotalSeconds(timeSpan);

            if ((totalSeconds > 0xFFFF) || (totalSeconds < 1))
                throw new ArgumentOutOfRangeException("timeSpan");

            m_timerWheel = timerWheel;
            SpanSeconds = totalSeconds;
            SectorIndex = 0xFFFF;
        }

        public int DecrementCurrentCount()
        {
            int currentCount = (int)((m_state & CurrentCountMask) >> CurrentCountShift);
            --currentCount;
            m_state &= ~CurrentCountMask;
            m_state |= (ulong)(currentCount << CurrentCountShift) & CurrentCountMask;
            return currentCount;
        }

        internal void OnExpired(object state)
        {
            if (Expired != null)
            {
                Expired(this, EventArgs.Empty);
            }
        }

        public void Reset()
        {
            Reset(TimerSpan);
        }

        public void Reset(TimeSpan newTimerSpan)
        {
            int totalSeconds = TotalSeconds(newTimerSpan);

            if ((totalSeconds > 0xFFFF) || (totalSeconds < 1))
                throw new ArgumentOutOfRangeException("newTimerSpan");

            lock (this)
            {
                if (IsStarted)
                {
                    m_timerWheel.Remove(this);
                }
                SpanSeconds = totalSeconds;
                SectorIndex = 0xFFFF;
                m_timerWheel.Add(this);
            }
        }

        public void Start()
        {
            lock (this)
            {
                if (!IsStarted)
                {
                    m_timerWheel.Add(this);
                }
            }
        }

        public void Stop()
        {
            lock (this)
            {
                if (IsStarted)
                {
                    m_timerWheel.Remove(this);
                }
            }
        }

        internal static int TotalSeconds(TimeSpan timeSpan)
        {
            return (int)(timeSpan.Ticks / TimeSpan.TicksPerSecond);
        }

        public int CurrentCount
        {
            get
            {
                return (int)((m_state & CurrentCountMask) >> CurrentCountShift);
            }
            set
            {
                if (value > 0xFF)
                    throw new ArgumentOutOfRangeException("value");

                m_state &= ~CurrentCountMask;
                m_state |= (((ulong)value) << CurrentCountShift) & CurrentCountMask;
            }
        }

        public bool IsStarted
        {
            get { return (SectorIndex != 0xFFFF); }
        }

        public TimeSpan RemainingTime
        {
            get { return m_timerWheel.GetRemainingTimeToExpire(this); }
        }

        public int SectorIndex
        {
            get
            {
                return (int)((m_state & SectorIndexMask) >> SectorIndexShift);
            }
            set
            {
                if (value > 0xFFFF)
                    throw new ArgumentOutOfRangeException("value");

                m_state &= ~SectorIndexMask;
                m_state |= (ulong)(((ulong)value << SectorIndexShift) & SectorIndexMask);
            }
        }

        public int SectorListIndex
        {
            get
            {
                return (int)(m_state & SectorListIndexMask);
            }
            set
            {
                m_state &= ~SectorListIndexMask;
                m_state |= (ulong)value & SectorListIndexMask;
            }
        }

        public int SpanSeconds
        {
            get
            {
                return (int)((m_state & SpanSecondsMask) >> SpanSecondsShift);
            }
            private set
            {
                if (value > 0xFFFF)
                    throw new ArgumentOutOfRangeException("value");

                m_state &= ~SpanSecondsMask;
                m_state |= (ulong)((ulong)(value << SpanSecondsShift) & SpanSecondsMask);
            }
        }

        public TimeSpan TimerSpan
        {
            get { return new TimeSpan(0, 0, SpanSeconds); }
        }

        public TimerWheel Wheel
        {
            get { return m_timerWheel; }
        }
    }
}
