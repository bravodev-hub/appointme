import { ProBadge } from './pro-badge';
import { useCurrentUser } from '@/components/auth';
import {
    Avatar,
    AvatarFallback,
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuGroup,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
    SidebarMenu,
    SidebarMenuButton,
    SidebarMenuItem,
    useSidebar,
} from '@/components/ui';
import { logout } from '@/lib/logout';
import { BellIcon, CreditCardIcon, EllipsisVerticalIcon, LogOutIcon, UserCircleIcon } from 'lucide-react';

export const NavUser = () => {
    const { isMobile, setOpenMobile } = useSidebar();
    const closeOnMobile = () => isMobile && setOpenMobile(false);
    const currentUser = useCurrentUser();

    return (
        <SidebarMenu>
            <SidebarMenuItem>
                <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                        <SidebarMenuButton
                            size='lg'
                            className='data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground'
                        >
                            <Avatar className='h-8 w-8 rounded-lg grayscale'>
                                <AvatarFallback className='rounded-lg'>{currentUser.initials}</AvatarFallback>
                            </Avatar>
                            <div className='grid flex-1 text-left text-sm leading-tight'>
                                <span className='truncate font-medium'>{currentUser.name}</span>
                                <span className='text-muted-foreground truncate text-xs'>{currentUser.email}</span>
                            </div>
                            <EllipsisVerticalIcon className='ml-auto size-4' />
                        </SidebarMenuButton>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent
                        className='w-(--radix-dropdown-menu-trigger-width) min-w-56 rounded-lg'
                        side={isMobile ? 'bottom' : 'right'}
                        align='end'
                        sideOffset={4}
                    >
                        <DropdownMenuLabel className='p-0 font-normal'>
                            <div className='flex items-center gap-2 px-1 py-1.5 text-left text-sm'>
                                <Avatar className='h-8 w-8 rounded-lg'>
                                    <AvatarFallback className='rounded-lg'>{currentUser.initials}</AvatarFallback>
                                </Avatar>
                                <div className='grid flex-1 text-left text-sm leading-tight'>
                                    <span className='truncate font-medium'>{currentUser.name}</span>
                                    <span className='text-muted-foreground truncate text-xs'>{currentUser.email}</span>
                                </div>
                            </div>
                        </DropdownMenuLabel>
                        <DropdownMenuSeparator />
                        <DropdownMenuGroup>
                            <DropdownMenuItem onClick={closeOnMobile}>
                                <UserCircleIcon />
                                Account
                                <ProBadge />
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={closeOnMobile}>
                                <CreditCardIcon />
                                Billing
                                <ProBadge />
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={closeOnMobile}>
                                <BellIcon />
                                Notifications
                                <ProBadge />
                            </DropdownMenuItem>
                        </DropdownMenuGroup>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem onClick={logout}>
                            <LogOutIcon />
                            Log out
                        </DropdownMenuItem>
                    </DropdownMenuContent>
                </DropdownMenu>
            </SidebarMenuItem>
        </SidebarMenu>
    );
};
