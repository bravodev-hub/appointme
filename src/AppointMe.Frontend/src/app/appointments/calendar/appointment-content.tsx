import { AppointmentDto } from '@/api/client.schemas';
import { FormattedDate } from '@/components/format';
import { cn } from '@/lib/utils';
import type { EventDisplayInfo } from '@fullcalendar/react';
import { cva } from 'class-variance-authority';
import { StickyNoteIcon } from 'lucide-react';

const cardVariants = cva('flex min-w-0 overflow-hidden', {
    variants: {
        status: {
            scheduled: 'bg-background border-l-foreground text-foreground',
            cancelled: 'bg-muted border-l-muted-foreground text-muted-foreground',
        },
    },
    defaultVariants: { status: 'scheduled' },
});

const dotVariants = cva('size-1.5 shrink-0 rounded-full', {
    variants: {
        status: {
            scheduled: 'bg-foreground',
            cancelled: 'bg-muted-foreground',
        },
    },
});

const attendeeStrikeVariants = cva('', {
    variants: {
        status: { scheduled: '', cancelled: 'line-through' },
    },
});

export const AppointmentContent = ({ arg }: { arg: EventDisplayInfo }) => {
    const appointment = arg.event.extendedProps as AppointmentDto;
    const status = appointment.status === 'Cancelled' ? 'cancelled' : 'scheduled';

    if (arg.view.type === 'dayGridMonth') {
        return (
            <div
                className={cn(
                    cardVariants({ status }),
                    'items-center gap-1.5 truncate rounded border-l-2 px-1.5 py-0.5 font-mono text-[10.5px]',
                )}
            >
                {arg.event.start && <FormattedDate date={arg.event.start} format='time24' />}
                <span className={cn('truncate', attendeeStrikeVariants({ status }))}>{appointment.attendeeName}</span>
            </div>
        );
    }

    return (
        <div className={cn(cardVariants({ status }), 'h-full flex-col gap-0.5 rounded-md border-l-[3px] px-2 py-1.5')}>
            <div className='flex min-w-0 items-center gap-1.5'>
                <span className={dotVariants({ status })} />
                <span
                    className={cn('truncate text-[12px] leading-tight font-medium', attendeeStrikeVariants({ status }))}
                >
                    {appointment.attendeeName}
                </span>
                {appointment.notes && <StickyNoteIcon className='ml-auto size-2.5 shrink-0 opacity-70' />}
            </div>
            <div className='font-mono text-[10.5px] leading-tight'>
                {arg.event.start && <FormattedDate date={arg.event.start} format='time24' />}
            </div>
            <div className='truncate text-[11px] leading-tight'>{appointment.providerName}</div>
        </div>
    );
};
