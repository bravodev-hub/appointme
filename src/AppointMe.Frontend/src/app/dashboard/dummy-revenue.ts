import { TrendBucketDto } from '@/api/appointme.schemas';

// Template placeholder: AppointMe has no revenue/payments domain yet, so the
// dashboard's revenue numbers are deterministic fake data derived from real
// appointment counts. Seeded by companyId + bucket date, they are stable across
// reloads and plausibly track the real booking trend. Replace this module with
// a real revenue stats endpoint once a money domain exists — nothing fake ever
// touches the API contract.
const AVERAGE_TICKET_MIN = 60;
const AVERAGE_TICKET_SPREAD = 30;

const hashSeed = (value: string): number => {
    let hash = 2166136261;
    for (let index = 0; index < value.length; index++) {
        hash ^= value.charCodeAt(index);
        hash = Math.imul(hash, 16777619);
    }
    return hash >>> 0;
};

// mulberry32, single draw
const randomFor = (seed: number): number => {
    const state = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};

export const revenueForBucket = (companyId: string, bucket: TrendBucketDto): number => {
    const random = randomFor(hashSeed(`${companyId}|${bucket.bucketStart}`));
    const averageTicket = AVERAGE_TICKET_MIN + random * AVERAGE_TICKET_SPREAD;
    return Math.round(bucket.appointments * averageTicket);
};

export const totalRevenue = (companyId: string, buckets: readonly TrendBucketDto[]): number =>
    buckets.reduce((sum, bucket) => sum + revenueForBucket(companyId, bucket), 0);
