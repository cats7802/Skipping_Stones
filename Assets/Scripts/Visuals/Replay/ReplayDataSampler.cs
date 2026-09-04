using System.Collections.Generic;
using UnityEngine;

namespace SkippingStones.Visuals.Replay
{
    /// <summary>
    /// 📊 리플레이 비행 데이터 수집 및 가공 전담 모듈
    /// </summary>
    public class ReplayDataSampler
    {
        public struct FlightSample
        {
            public Vector3 position;
            public bool isRingBoost;
        }

        private readonly List<FlightSample> realTimeFlightTrajectory = new List<FlightSample>();
        private float lastSampleZ = -999f;

        public IReadOnlyList<FlightSample> FlightTrajectory => realTimeFlightTrajectory;

        public void Reset(Vector3 startOrigin)
        {
            realTimeFlightTrajectory.Clear();
            lastSampleZ = startOrigin.z;
            realTimeFlightTrajectory.Add(new FlightSample { position = startOrigin, isRingBoost = false });
        }

        public void SamplePosition(Vector3 pos, bool isRingBoost = false)
        {
            if (realTimeFlightTrajectory.Count == 0 || 
                Mathf.Abs(pos.z - lastSampleZ) >= 2.0f || 
                (pos - realTimeFlightTrajectory[realTimeFlightTrajectory.Count - 1].position).sqrMagnitude >= 4.0f)
            {
                realTimeFlightTrajectory.Add(new FlightSample { position = pos, isRingBoost = isRingBoost });
                lastSampleZ = pos.z;
            }
        }

        public List<SkippingStone.BounceRecord> BuildMarkerRecords(
            List<SkippingStone.BounceRecord> rawBounces, 
            Vector3 startOrigin, 
            float finalDistance)
        {
            var records = new List<SkippingStone.BounceRecord>();

            if (rawBounces != null && rawBounces.Count > 0)
            {
                records.AddRange(rawBounces);
            }

            if (realTimeFlightTrajectory.Count > 0)
            {
                bool wasInRing = false;
                for (int s = 0; s < realTimeFlightTrajectory.Count; s++)
                {
                    var sample = realTimeFlightTrajectory[s];
                    if (sample.isRingBoost && !wasInRing)
                    {
                        wasInRing = true;
                        records.Add(new SkippingStone.BounceRecord
                        {
                            position = sample.position,
                            skipIndex = 9000 + s,
                            grade = "RING_BOOST",
                            distance = sample.position.z
                        });
                    }
                    else if (!sample.isRingBoost)
                    {
                        wasInRing = false;
                    }
                }
            }

            records.Sort((a, b) => a.distance.CompareTo(b.distance));

            if (records.Count == 0 || records[0].grade != "START")
            {
                records.Insert(0, new SkippingStone.BounceRecord { position = startOrigin, skipIndex = 0, grade = "START", distance = 0f });
            }
            if (records.Count == 1 || records[records.Count - 1].grade != "FINISH")
            {
                Vector3 finishPos = (realTimeFlightTrajectory.Count > 0) 
                    ? realTimeFlightTrajectory[realTimeFlightTrajectory.Count - 1].position 
                    : startOrigin + Vector3.forward * finalDistance;
                records.Add(new SkippingStone.BounceRecord { position = finishPos, skipIndex = records.Count, grade = "FINISH", distance = finalDistance });
            }

            return records;
        }

        public List<Vector3> BuildTrajectoryPathPoints(List<SkippingStone.BounceRecord> markerRecords, float baseReplayLevel)
        {
            var pathPoints = new List<Vector3>();

            if (realTimeFlightTrajectory.Count >= 2)
            {
                for (int i = 0; i < realTimeFlightTrajectory.Count; i++)
                {
                    pathPoints.Add(new Vector3(realTimeFlightTrajectory[i].position.x, baseReplayLevel + 0.15f, realTimeFlightTrajectory[i].position.z));
                }
            }
            else if (markerRecords != null)
            {
                for (int i = 0; i < markerRecords.Count; i++)
                {
                    pathPoints.Add(new Vector3(markerRecords[i].position.x, baseReplayLevel + 0.15f, markerRecords[i].position.z));
                }
            }

            return pathPoints;
        }
    }
}
