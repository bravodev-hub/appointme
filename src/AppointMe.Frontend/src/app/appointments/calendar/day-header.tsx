import { FormattedDate } from '@/components/format';
import { cn } from '@/lib/utils';
import type { DayHeaderInfo } from '@fullcalendar/react';

export const DayHeader = ({ arg }: { arg: DayHeaderInfo }) => {
    if (arg.inPopover) {
        return <FormattedDate date={arg.date} format='weekdayDayMonthShort' className='text-xs font-medium' />;
    }

    if (arg.view.type === 'dayGridMonth') {
        return (
            <FormattedDate
                date={arg.date}
                format='weekday'
                className='text-muted-foreground text-[11px] font-medium tracking-wider uppercase'
            />
        );
    }

    return (
        <div className='flex flex-col items-start gap-1'>
            <FormattedDate
                date={arg.date}
                format='weekday'
                className={cn(
                    'text-[10.5px] font-medium tracking-wider uppercase',
                    arg.isToday ? 'text-foreground' : 'text-muted-foreground',
                )}
            />
            <span
                className={cn(
                    'inline-flex items-center justify-center text-lg leading-none font-semibold',
                    arg.isToday && 'bg-foreground text-background size-7 rounded-full',
                )}
            >
                {arg.date.getDate()}
            </span>
        </div>
    );
};
