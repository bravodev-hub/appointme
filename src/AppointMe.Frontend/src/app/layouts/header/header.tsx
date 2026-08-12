import { CompanySelector } from './company-selector';
import { useBreadcrumbs } from '@/app/router';
import { ModeToggle } from '@/components/theme';
import {
    Breadcrumb,
    BreadcrumbItem,
    BreadcrumbLink,
    BreadcrumbList,
    BreadcrumbPage,
    BreadcrumbSeparator,
    Separator,
    SidebarTrigger,
} from '@/components/ui';
import { Fragment } from 'react';
import { Link } from 'react-router';

export const Header = () => {
    const breadcrumbs = useBreadcrumbs();

    return (
        <header className='flex h-16 shrink-0 items-center gap-2 border-b px-4'>
            <SidebarTrigger className='-ml-1' />
            <Separator orientation='vertical' className='mr-2 data-[orientation=vertical]:h-4' />
            <Breadcrumb>
                <BreadcrumbList>
                    {breadcrumbs.map((crumb, index) => (
                        <Fragment key={crumb.title}>
                            {index > 0 && <BreadcrumbSeparator />}
                            <BreadcrumbItem>
                                {index < breadcrumbs.length - 1 ? (
                                    <BreadcrumbLink asChild>
                                        {crumb.url ? (
                                            <Link to={crumb.url}>{crumb.title}</Link>
                                        ) : (
                                            <span>{crumb.title}</span>
                                        )}
                                    </BreadcrumbLink>
                                ) : (
                                    <BreadcrumbPage>{crumb.title}</BreadcrumbPage>
                                )}
                            </BreadcrumbItem>
                        </Fragment>
                    ))}
                </BreadcrumbList>
            </Breadcrumb>
            <div className='ml-auto flex h-5 items-center gap-4 text-sm'>
                <CompanySelector />
                <Separator orientation='vertical' />
                <ModeToggle />
            </div>
        </header>
    );
};
