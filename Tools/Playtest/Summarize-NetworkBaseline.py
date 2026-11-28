"""Summarize the per-peer Development Relay telemetry without parsing private Player logs."""

import argparse
import csv
import json
import math
import statistics
from collections import defaultdict
from pathlib import Path


def load(path):
    with path.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def percentile(values, fraction):
    return sorted(values)[math.ceil(len(values) * fraction) - 1]


def distribution(values):
    if not values:
        return None
    return {
        "samples": len(values),
        "mean": statistics.mean(values),
        "median": statistics.median(values),
        "p95": percentile(values, .95),
        "p99": percentile(values, .99),
        "max": max(values),
    }


def groups(rows, keys):
    grouped = defaultdict(list)
    for row in rows:
        grouped[tuple(row[key] for key in keys)].append(row)
    return grouped


def summarize(samples, frames):
    first, last = samples[0], samples[-1]
    seconds = float(last["time"]) - float(first["time"])
    if seconds <= 0:
        return None
    driver_counters = ("txBytes", "rxBytes", "txPackets", "rxPackets")
    changes = {key: int(last[key]) - int(first[key]) for key in driver_counters}
    if any(value < 0 for value in changes.values()):
        raise ValueError(f"Counter reset in {first['segment']} generation {first['generation']}")
    reliable_counters = ("reliableSent", "reliableReceived", "reliableDropped", "reliableResent")
    reliable_changes = dict.fromkeys(reliable_counters, 0)
    reliable_intervals = 0
    for left, right in zip(samples, samples[1:]):
        if left["connected"] != "1" or right["connected"] != "1":
            continue
        deltas = {key: int(right[key]) - int(left[key]) for key in reliable_counters}
        if any(value < 0 for value in deltas.values()):
            continue
        reliable_intervals += 1
        for key, value in deltas.items():
            reliable_changes[key] += value
    valid_rtt = [row for row in samples if row["connected"] == "1" and float(row["rttMs"]) > 0]
    rtt = [float(row["rttMs"]) for row in valid_rtt]
    selection = [
        row for row in frames
        if row["segment"] == first["segment"] and float(first["time"]) <= float(row["time"]) <= float(last["time"])
    ]
    frame_ms = [float(row["frameMs"]) for row in selection]
    gpu_ms = [float(row["gpuMs"]) for row in selection if float(row["gpuMs"]) > 0]
    markers = {}
    if selection:
        for column in selection[0]:
            if not column.endswith(".count"):
                continue
            marker = column.removesuffix(".count")
            count = [int(row[column]) for row in selection if row[column]]
            values = [int(row[marker + ".value"]) for row in selection if row[marker + ".value"]]
            markers[marker] = {"samples": sum(count), "valueSum": sum(values)} if count else None
    return {
        "windowSeconds": seconds,
        "transportSamples": len(samples),
        "excludedRttSamples": len(samples) - len(rtt),
        "lastRttMs": distribution(rtt),
        "driverCurrentRttMs": distribution([float(row["rawRttMs"]) for row in valid_rtt if float(row["rawRttMs"]) > 0]),
        "driverSmoothedRttMs": distribution([
            float(row.get("utpSmoothedMs", 0)) for row in valid_rtt if float(row.get("utpSmoothedMs", 0)) > 0
        ]),
        "successiveRttAbsoluteChangeMs": distribution([abs(right - left) for left, right in zip(rtt, rtt[1:])]),
        "driverDeltas": changes,
        "reliableDeltas": reliable_changes,
        "reliableIntervals": reliable_intervals,
        "driverBytesPerSecond": {direction: changes[direction + "Bytes"] / seconds for direction in ("tx", "rx")},
        "observedSettings": sorted({
            (int(row["delayMs"]), int(row["jitterMs"]), float(row["sendLossPercent"]), float(row["receiveLossPercent"]))
            for row in samples
        }),
        "frameMs": distribution(frame_ms),
        "gpuMs": distribution(gpu_ms),
        "objects": sorted({int(row["objects"]) for row in samples}),
        "probeMsPerFrame": sum(float(row["probeMs"]) for row in samples[1:]) / len(selection) if selection else None,
        "markers": markers,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", nargs="+", type=Path)
    args = parser.parse_args()
    report = {}
    for path in args.run:
        peers = {}
        for peer in ("host", "client"):
            frames = load(path / (peer + "-network-frames.csv"))
            samples = load(path / (peer + "-network.csv"))
            peers[peer] = {
                f"{segment}/{generation}": summarize(rows, frames)
                for (segment, generation), rows in groups(samples, ("segment", "generation")).items()
                if segment != "transition"
            }
        report[path.name] = peers
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
