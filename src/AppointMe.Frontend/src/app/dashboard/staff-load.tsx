import { PERIOD_LABELS, PeriodPreset } from './use-dashboard-period';
import { StaffLoadDto } from '@/api/appointme.schemas';
import {
    Avatar,
    AvatarFallback,
    Card,
    CardContent,
    CardHeader,
    CardTitle,
    Empty,
    EmptyDescription,
    EmptyHeader,
    EmptyTitle,
    Skeleton,
} from '@/components/ui';

interface StaffLoadProps {
    staffLoad: StaffLoadDto[] | undefined;
    preset: PeriodPreset;
}

const initialsFor = (name: string): string =>
    name
        .split(' ')
        .filter(part => part.length > 0)
        .map(part => part[0])
        .slice(0, 2)
        .join('')
        .toUpperCase();

// Monochrome ramp for row bars — same token, stepped opacity.
const STAFF_BAR_OPACITY = [1, 0.8, 0.65, 0.5, 0.4];

const utilizationClass = (percent: number): string => {
    if (percent >= 90) {
        return 'text-red-700 dark:text-red-400';
    }
    if (percent >= 75) {
        return 'text-emerald-700 dark:text-emerald-400';
    }
    return 'text-muted-foreground';
};

export const StaffLoad = ({ staffLoad, preset }: StaffLoadProps) => {
    if (!staffLoad) {
        return <Skeleton className='h-[300px]' />;
    }

    const max = Math.max(...staffLoad.map(staff => staff.bookings), 1);

    return (
        <Card>
            <CardHeader className='flex items-baseline justify-between'>
                <CardTitle className='text-sm'>Bookings by staff</CardTitle>
                <span className='text-muted-foreground/70 text-xs'>{PERIOD_LABELS[preset]}</span>
            </CardHeader>
            <CardContent className='flex flex-col gap-3.5'>
                {staffLoad.length === 0 && (
                    <Empty>
                        <EmptyHeader>
                            <EmptyTitle>No staff yet</EmptyTitle>
                            <EmptyDescription>Add team members to see their booking load.</EmptyDescription>
                        </EmptyHeader>
                    </Empty>
                )}
                {staffLoad.map((staff, index) => {
                    const utilization =
                        staff.capacityMinutes > 0
                            ? Math.round((staff.bookedMinutes / staff.capacityMinutes) * 100)
                            : null;
                    return (
                        <div key={staff.providerId} className='flex items-center gap-3'>
                            <Avatar className='size-8'>
                                <AvatarFallback className='text-xs'>{initialsFor(staff.name)}</AvatarFallback>
                            </Avatar>
                            <div className='min-w-0 flex-1'>
                                <div className='mb-1 flex items-baseline justify-between gap-2'>
                                    <span className='truncate text-[13px] font-medium'>{staff.name}</span>
                                    <span className='flex items-baseline gap-2'>
                                        <span className='font-mono text-[13px] font-medium'>{staff.bookings}</span>
                                        {utilization != null && (
                                            <span
                                                className={`text-[11px] font-medium ${utilizationClass(utilization)}`}
                                            >
                                                {utilization}% util
                                            </span>
                                        )}
                                    </span>
                                </div>
                                <div className='bg-muted h-1.5 overflow-hidden rounded-full'>
                                    <div
                                        className='bg-foreground h-full rounded-full transition-[width]'
                                        style={{
                                            width: `${(staff.bookings / max) * 100}%`,
                                            opacity: STAFF_BAR_OPACITY[index % STAFF_BAR_OPACITY.length],
                                        }}
                                    />
                                </div>
                            </div>
                        </div>
                    );
                })}
            </CardContent>
        </Card>
    );
};
