import { AppointmentDto } from '@/api/client.schemas';
import { Can } from '@/components/auth/can';
import { FormattedDate } from '@/components/format';
import { Avatar, AvatarFallback, Button, Popover, PopoverContent, PopoverTrigger } from '@/components/ui';
import { cn } from '@/lib/utils';
import { CalendarIcon, ClockIcon, StickyNoteIcon, UserIcon, XIcon } from 'lucide-react';

type AppointmentPreviewProps = {
    appointment: AppointmentDto;
    x: number;
    y: number;
    onClose: () => void;
    onReschedule: () => void;
    onCancel: () => void;
};

export const AppointmentPreview = ({ appointment, x, y, onClose, onReschedule, onCancel }: AppointmentPreviewProps) => {
    const isCancelled = appointment.status === 'Cancelled';
    return (
        <Popover open onOpenChange={open => !open && onClose()}>
            <PopoverTrigger asChild>
                <span className='pointer-events-none fixed' style={{ left: x, top: y, width: 1, height: 1 }} />
            </PopoverTrigger>
            <PopoverContent
                side='bottom'
                align='start'
                sideOffset={8}
                collisionPadding={16}
                className='w-76 overflow-hidden p-0'
            >
                <div className='border-border border-b p-4'>
                    <div className='mb-2 flex items-center justify-between'>
                        <span className='bg-muted border-border inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-[11px] font-medium'>
                            <span
                                className={cn(
                                    'size-1.5 rounded-full',
                                    isCancelled ? 'bg-muted-foreground' : 'bg-foreground',
                                )}
                            />
                            {isCancelled ? 'Cancelled' : 'Scheduled'}
                        </span>
                        <button
                            type='button'
                            onClick={onClose}
                            aria-label='Close'
                            className='text-muted-foreground hover:text-foreground inline-flex size-6 items-center justify-center rounded'
                        >
                            <XIcon className='size-3.5' />
                        </button>
                    </div>
                    <div className='flex items-center gap-2.5'>
                        <Avatar className='size-9'>
                            <AvatarFallback className='text-xs'>{appointment.attendeeInitials}</AvatarFallback>
                        </Avatar>
                        <div className='min-w-0'>
                            <div className='truncate text-sm leading-tight font-semibold'>
                                {appointment.attendeeName}
                            </div>
                        </div>
                    </div>
                </div>
                <div className='flex flex-col gap-2 p-4 text-[12.5px]'>
                    <PreviewRow icon={<CalendarIcon className='size-3.5' />} label='Date'>
                        <FormattedDate date={appointment.start} format='weekdayDayMonthShort' />
                    </PreviewRow>
                    <PreviewRow icon={<ClockIcon className='size-3.5' />} label='Time'>
                        <span className='font-mono'>
                            <FormattedDate date={appointment.start} format='time24' />–
                            <FormattedDate date={appointment.end} format='time24' />
                        </span>
                    </PreviewRow>
                    <PreviewRow icon={<UserIcon className='size-3.5' />} label='Staff'>
                        {appointment.providerName}
                    </PreviewRow>
                    {appointment.notes && (
                        <PreviewRow icon={<StickyNoteIcon className='size-3.5' />} label='Notes'>
                            <span className='text-muted-foreground italic'>{appointment.notes}</span>
                        </PreviewRow>
                    )}
                </div>
                {!isCancelled && (
                    <div className='bg-muted border-border flex gap-1.5 border-t p-2.5'>
                        <Can permission='appointments:reschedule'>
                            <Button variant='outline' size='sm' className='flex-1' onClick={onReschedule}>
                                Reschedule
                            </Button>
                        </Can>
                        <Can permission='appointments:cancel'>
                            <Button variant='outline' size='sm' className='flex-1' onClick={onCancel}>
                                Cancel
                            </Button>
                        </Can>
                    </div>
                )}
            </PopoverContent>
        </Popover>
    );
};

const PreviewRow = ({ icon, label, children }: { icon: React.ReactNode; label: string; children: React.ReactNode }) => (
    <div className='flex gap-2.5'>
        <div className='text-muted-foreground flex w-15 shrink-0 items-center gap-1.5'>
            {icon}
            {label}
        </div>
        <div className='min-w-0 flex-1'>{children}</div>
    </div>
);
