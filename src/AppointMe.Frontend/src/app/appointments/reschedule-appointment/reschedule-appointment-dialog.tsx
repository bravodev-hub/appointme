import { useGetServiceProviders, useRescheduleAppointment } from '@/api/appointme';
import { AppointmentDto } from '@/api/appointme.schemas';
import { parseInputDateTime, toDateInputValue, toTimeInputValue, useTimeZone } from '@/components/format';
import { SubmitButton } from '@/components/form';
import { IModalProps } from '@/components/modal-dialog';
import { Button, Input } from '@/components/ui';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import * as z from 'zod';

const formSchema = z.object({
    providerId: z.string().min(1, 'Provider is required'),
    date: z.string().min(1, 'Date is required'),
    startTime: z.string().min(1, 'Start time is required'),
    endTime: z.string().min(1, 'End time is required'),
});

type FormValues = z.infer<typeof formSchema>;

interface RescheduleAppointmentDialogProps extends IModalProps<boolean> {
    appointment: AppointmentDto;
}

export const RescheduleAppointmentDialog = ({ resolve, visible, appointment }: RescheduleAppointmentDialogProps) => {
    const timeZone = useTimeZone();
    const { data: providers } = useGetServiceProviders();
    const { mutateAsync: reschedule } = useRescheduleAppointment();

    const { control, formState, handleSubmit } = useForm<FormValues>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            providerId: appointment.providerId,
            date: toDateInputValue(appointment.start, timeZone),
            startTime: toTimeInputValue(appointment.start, timeZone),
            endTime: toTimeInputValue(appointment.end, timeZone),
        },
    });

    const onSubmit = async (data: FormValues) => {
        const start = parseInputDateTime(data.date, data.startTime, timeZone);
        const end = parseInputDateTime(data.date, data.endTime, timeZone);

        await reschedule({
            id: appointment.id,
            data: { providerId: data.providerId, start, end },
        });
        toast.success('Appointment rescheduled.');
        resolve(true);
    };

    return (
        <Dialog open={visible} onOpenChange={open => !open && resolve(false)}>
            <DialogTrigger asChild>
                <button type='button' aria-hidden tabIndex={-1} className='hidden' />
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Reschedule appointment</DialogTitle>
                    <DialogDescription>Reschedule appointment for {appointment.attendeeName}.</DialogDescription>
                </DialogHeader>
                <form id='reschedule-appointment-form' onSubmit={handleSubmit(onSubmit)}>
                    <FieldGroup>
                        <Controller
                            name='providerId'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='providerId'>
                                        Service provider <span className='text-destructive'>*</span>
                                    </FieldLabel>
                                    <Select name='providerId' value={field.value} onValueChange={field.onChange}>
                                        <SelectTrigger id='providerId' aria-label='Service provider' className='w-full'>
                                            <SelectValue placeholder='Select provider' />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {(providers ?? []).map(p => (
                                                <SelectItem key={p.id} value={p.id}>
                                                    {p.fullName}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />

                        <Controller
                            name='date'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='date'>
                                        Date <span className='text-destructive'>*</span>
                                    </FieldLabel>
                                    <Input id='date' aria-label='Date' {...field} type='date' />
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />

                        <div className='grid grid-cols-2 gap-4'>
                            <Controller
                                name='startTime'
                                control={control}
                                render={({ field, fieldState }) => (
                                    <Field data-invalid={fieldState.invalid}>
                                        <FieldLabel htmlFor='startTime'>
                                            Start <span className='text-destructive'>*</span>
                                        </FieldLabel>
                                        <Input id='startTime' aria-label='Start time' {...field} type='time' />
                                        {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                    </Field>
                                )}
                            />
                            <Controller
                                name='endTime'
                                control={control}
                                render={({ field, fieldState }) => (
                                    <Field data-invalid={fieldState.invalid}>
                                        <FieldLabel htmlFor='endTime'>
                                            End <span className='text-destructive'>*</span>
                                        </FieldLabel>
                                        <Input id='endTime' aria-label='End time' {...field} type='time' />
                                        {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                    </Field>
                                )}
                            />
                        </div>
                    </FieldGroup>
                </form>
                <DialogFooter>
                    <Button type='button' variant='outline' onClick={() => resolve(false)}>
                        Cancel
                    </Button>
                    <SubmitButton
                        form='reschedule-appointment-form'
                        isSubmitting={formState.isSubmitting}
                        submittingLabel='Rescheduling...'
                    >
                        Reschedule
                    </SubmitButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
