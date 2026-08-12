import { useGetCustomers, useGetServiceProviders, useScheduleAppointment } from '@/api/appointme';
import { CustomerDto } from '@/api/appointme.schemas';
import { parseInputDateTime, toDateInputValue, toTimeInputValue, useTimeZone } from '@/components/format';
import { SubmitButton } from '@/components/form';
import { IModalProps } from '@/components/modal-dialog';
import { Avatar, AvatarFallback, Button, Input } from '@/components/ui';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
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
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import { zodResolver } from '@hookform/resolvers/zod';
import { useDebouncedValue } from '@tanstack/react-pacer/debouncer';
import { ChevronsUpDownIcon } from 'lucide-react';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import * as z from 'zod';

const NOTES_MAX_LENGTH = 4000;

const formSchema = z.object({
    attendeeId: z.string().min(1, 'Client is required'),
    date: z.string().min(1, 'Date is required'),
    startTime: z.string().min(1, 'Start time is required'),
    endTime: z.string().min(1, 'End time is required'),
    providerId: z.string().min(1, 'Staff is required'),
    notes: z
        .string()
        .max(NOTES_MAX_LENGTH, `Notes must be ${NOTES_MAX_LENGTH} characters at max.`)
        .optional()
        .nullable(),
});

type FormValues = z.infer<typeof formSchema>;

interface ScheduleAppointmentDialogProps extends IModalProps<boolean> {
    initialSlot?: { start: Date; end: Date };
}

export const ScheduleAppointmentDialog = ({ resolve, visible, initialSlot }: ScheduleAppointmentDialogProps) => {
    const timeZone = useTimeZone();
    const [clientSearch, setClientSearch] = useState('');
    const [debouncedSearch] = useDebouncedValue(clientSearch, { wait: 300 });
    const [clientPopoverOpen, setClientPopoverOpen] = useState(false);
    const [selectedClient, setSelectedClient] = useState<CustomerDto | null>(null);

    const { data: providers } = useGetServiceProviders();
    const { data: customersResponse } = useGetCustomers({
        search: debouncedSearch || undefined,
        page: 1,
        limit: 20,
    });
    const customers = customersResponse?.customers.items ?? [];
    const { mutateAsync: schedule } = useScheduleAppointment();

    const { control, formState, handleSubmit } = useForm<FormValues>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            providerId: '',
            attendeeId: '',
            date: toDateInputValue(initialSlot?.start, timeZone),
            startTime: toTimeInputValue(initialSlot?.start, timeZone),
            endTime: toTimeInputValue(initialSlot?.end, timeZone),
            notes: '',
        },
    });

    const onSubmit = async (data: FormValues) => {
        const start = parseInputDateTime(data.date, data.startTime, timeZone);
        const end = parseInputDateTime(data.date, data.endTime, timeZone);

        await schedule({
            data: {
                providerId: data.providerId,
                attendeeId: data.attendeeId,
                start,
                end,
                notes: data.notes || null,
            },
        });
        toast.success('Appointment scheduled.');
        resolve(true);
    };

    return (
        <Dialog open={visible} onOpenChange={open => !open && resolve(false)}>
            <DialogTrigger asChild>
                <button type='button' aria-hidden tabIndex={-1} className='hidden' />
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Schedule appointment</DialogTitle>
                    <DialogDescription>Pick a customer, staff member, and time slot.</DialogDescription>
                </DialogHeader>
                <form id='schedule-appointment-form' onSubmit={handleSubmit(onSubmit)}>
                    <FieldGroup>
                        <Controller
                            name='attendeeId'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel>
                                        Client <span className='text-destructive'>*</span>
                                    </FieldLabel>
                                    <Popover open={clientPopoverOpen} onOpenChange={setClientPopoverOpen}>
                                        <PopoverTrigger asChild>
                                            <Button
                                                type='button'
                                                variant='outline'
                                                role='combobox'
                                                aria-expanded={clientPopoverOpen}
                                                className={cn(
                                                    'w-full justify-between font-normal',
                                                    !selectedClient && 'text-muted-foreground',
                                                )}
                                            >
                                                {selectedClient ? selectedClient.fullName : 'Select client...'}
                                                <ChevronsUpDownIcon className='opacity-50' />
                                            </Button>
                                        </PopoverTrigger>
                                        <PopoverContent className='w-(--radix-popover-trigger-width) p-0' align='start'>
                                            <Command shouldFilter={false}>
                                                <CommandInput
                                                    placeholder='Search by name or email...'
                                                    value={clientSearch}
                                                    onValueChange={setClientSearch}
                                                />
                                                <CommandList>
                                                    <CommandEmpty>No clients found.</CommandEmpty>
                                                    <CommandGroup>
                                                        {customers.map(customer => (
                                                            <CommandItem
                                                                key={customer.id}
                                                                value={customer.id}
                                                                onSelect={() => {
                                                                    field.onChange(customer.id);
                                                                    setSelectedClient(customer);
                                                                    setClientPopoverOpen(false);
                                                                }}
                                                            >
                                                                <Avatar className='size-7'>
                                                                    <AvatarFallback className='text-xs'>
                                                                        {customer.initials}
                                                                    </AvatarFallback>
                                                                </Avatar>
                                                                <div className='flex flex-col'>
                                                                    <span>{customer.fullName}</span>
                                                                    {customer.email && (
                                                                        <span className='text-muted-foreground text-xs'>
                                                                            {customer.email}
                                                                        </span>
                                                                    )}
                                                                </div>
                                                            </CommandItem>
                                                        ))}
                                                    </CommandGroup>
                                                </CommandList>
                                            </Command>
                                        </PopoverContent>
                                    </Popover>
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

                        <Controller
                            name='providerId'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='providerId'>
                                        Staff <span className='text-destructive'>*</span>
                                    </FieldLabel>
                                    <Select value={field.value} onValueChange={field.onChange}>
                                        <SelectTrigger id='providerId' aria-label='Staff' className='w-full'>
                                            <SelectValue placeholder='Select staff' />
                                        </SelectTrigger>
                                        <SelectContent>
                                            {(providers ?? []).map(provider => (
                                                <SelectItem key={provider.id} value={provider.id}>
                                                    {provider.fullName}
                                                </SelectItem>
                                            ))}
                                        </SelectContent>
                                    </Select>
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />

                        <Controller
                            name='notes'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='notes'>Notes</FieldLabel>
                                    <Textarea
                                        id='notes'
                                        aria-label='Notes'
                                        {...field}
                                        value={field.value ?? ''}
                                        maxLength={NOTES_MAX_LENGTH}
                                        placeholder='Optional notes...'
                                    />
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />
                    </FieldGroup>
                </form>
                <DialogFooter>
                    <Button type='button' variant='outline' onClick={() => resolve(false)}>
                        Cancel
                    </Button>
                    <SubmitButton
                        form='schedule-appointment-form'
                        isSubmitting={formState.isSubmitting}
                        submittingLabel='Scheduling...'
                    >
                        Schedule
                    </SubmitButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
