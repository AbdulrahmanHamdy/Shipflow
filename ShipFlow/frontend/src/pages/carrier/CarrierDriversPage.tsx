import React, { useState } from 'react';
import {
  useDriversListQuery,
  useRegisterDriverMutation,
  useUpdateDriverMutation,
  useActivateDriverMutation,
  useDeactivateDriverMutation,
  useCompanyMembersQuery,
} from '@/features/carrier/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { DriverDto, AppError } from '@/api/types';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { Modal } from '@/components/ui/Modal';
import { FormField } from '@/components/ui/FormField';
import { Input } from '@/components/ui/Input';
import { Pagination } from '@/components/ui/Pagination';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { useToast } from '@/components/ui/Toast';
import { Users, Plus, Edit2, ShieldCheck, Power } from 'lucide-react';

export const CarrierDriversPage: React.FC = () => {
  usePageTitle('Company Drivers');
  const toast = useToast();
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const { data, isLoading, isError, error, refetch } = useDriversListQuery({ page, pageSize });
  const { data: members = [] } = useCompanyMembersQuery();

  const registerMutation = useRegisterDriverMutation();
  const updateMutation = useUpdateDriverMutation();
  const activateMutation = useActivateDriverMutation();
  const deactivateMutation = useDeactivateDriverMutation();

  // Eligible members for driver profile creation: members with role 'Driver'
  const driverMembers = members.filter((m) => m.role === 'Driver' && m.isActive);

  // Modals
  const [registerModalOpen, setRegisterModalOpen] = useState(false);
  const [editModalDriver, setEditModalDriver] = useState<DriverDto | null>(null);

  // Register form
  const [selectedUserId, setSelectedUserId] = useState('');
  const [fullName, setFullName] = useState('');
  const [licenseNumber, setLicenseNumber] = useState('');

  // Edit form
  const [editFullName, setEditFullName] = useState('');
  const [editLicense, setEditLicense] = useState('');

  const handleRegister = async () => {
    if (!selectedUserId || !fullName.trim() || !licenseNumber.trim()) {
      toast.error('All fields required', 'Please select a driver member and provide full name and license number.');
      return;
    }

    try {
      await registerMutation.mutateAsync({
        userId: selectedUserId,
        fullName: fullName.trim(),
        licenseNumber: licenseNumber.trim().toUpperCase(),
      });

      toast.success('Driver Profile Registered', `${fullName} is now an active company driver.`);
      setRegisterModalOpen(false);
      setSelectedUserId('');
      setFullName('');
      setLicenseNumber('');
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Registration refused', appErr.message);
    }
  };

  const handleOpenEdit = (d: DriverDto) => {
    setEditModalDriver(d);
    setEditFullName(d.fullName);
    setEditLicense(d.licenseNumber);
  };

  const handleUpdate = async () => {
    if (!editModalDriver) return;

    try {
      await updateMutation.mutateAsync({
        id: editModalDriver.id,
        command: {
          fullName: editFullName.trim(),
          licenseNumber: editLicense.trim().toUpperCase(),
        },
      });

      toast.success('Driver Updated', 'Driver profile information saved.');
      setEditModalDriver(null);
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Update failed', appErr.message);
    }
  };

  const handleToggleActive = async (d: DriverDto) => {
    try {
      if (d.isActive) {
        await deactivateMutation.mutateAsync(d.id);
        toast.info('Driver Deactivated', `${d.fullName} marked inactive.`);
      } else {
        await activateMutation.mutateAsync(d.id);
        toast.success('Driver Activated', `${d.fullName} is now active.`);
      }
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Status change refused', appErr.message);
    }
  };

  const drivers = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = data?.totalPages || 1;

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <PageHeader
        title="Fleet Drivers"
        description="Manage verified driver profiles and commercial transport licenses."
        breadcrumbs={[{ label: 'Control Tower', href: '/carrier' }, { label: 'Drivers' }]}
        actions={
          <Button
            variant="primary"
            size="sm"
            onClick={() => setRegisterModalOpen(true)}
            leftIcon={<Plus className="w-4 h-4" />}
          >
            Register Driver Profile
          </Button>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="p-6 space-y-3">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          ) : isError ? (
            <div className="p-6">
              <ErrorState message={(error as any)?.message} onRetry={() => refetch()} />
            </div>
          ) : drivers.length === 0 ? (
            <div className="p-8">
              <EmptyState
                icon={Users}
                title="No Drivers Registered"
                description="Register driver profiles for active company members to enable trip dispatch."
                actionLabel="Register Driver"
                onAction={() => setRegisterModalOpen(true)}
              />
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Full Name</TableHead>
                    <TableHead>License Number</TableHead>
                    <TableHead>User Account ID</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {drivers.map((d) => (
                    <TableRow key={d.id}>
                      <TableCell className="font-bold text-neutral-primary">
                        {d.fullName}
                      </TableCell>
                      <TableCell className="font-mono text-xs font-semibold text-neutral-secondary">
                        {d.licenseNumber}
                      </TableCell>
                      <TableCell className="font-mono text-[11px] text-neutral-secondary">
                        {d.userId.substring(0, 16)}...
                      </TableCell>
                      <TableCell>
                        <span
                          className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${
                            d.isActive
                              ? 'bg-emerald-50 text-emerald-800 border border-emerald-300'
                              : 'bg-neutral-hover text-neutral-muted'
                          }`}
                        >
                          {d.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          <Button
                            variant="outline"
                            size="sm"
                            className="h-7 text-xs"
                            onClick={() => handleOpenEdit(d)}
                            leftIcon={<Edit2 className="w-3 h-3" />}
                          >
                            Edit
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            className={`h-7 text-xs ${d.isActive ? 'text-amber-700 hover:bg-amber-50' : 'text-emerald-700 hover:bg-emerald-50'}`}
                            onClick={() => handleToggleActive(d)}
                          >
                            {d.isActive ? 'Deactivate' : 'Activate'}
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              <div className="p-4 border-t border-neutral-border">
                <Pagination
                  page={page}
                  totalPages={totalPages}
                  totalCount={totalCount}
                  pageSize={pageSize}
                  onPageChange={setPage}
                />
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Register Driver Modal */}
      <Modal
        isOpen={registerModalOpen}
        onClose={() => setRegisterModalOpen(false)}
        title="Register Driver Profile"
        description="Link an active company driver member to a verified commercial driving profile."
      >
        <div className="space-y-4">
          <FormField
            label="Driver Member Account"
            required
            hint="Member must already exist under Company Team with 'Driver' role"
            htmlFor="memberSelect"
          >
            {driverMembers.length === 0 ? (
              <div className="p-3 rounded-lg bg-amber-50 border border-amber-200 text-xs text-amber-900">
                No active members with the <strong>Driver</strong> role found. First add a team member with role 'Driver' under the Company &amp; Team section.
              </div>
            ) : (
              <select
                id="memberSelect"
                className="w-full h-10 px-3 py-2 rounded-lg border border-neutral-border bg-white text-sm text-neutral-primary focus:border-brand focus:ring-1 focus:ring-brand"
                value={selectedUserId}
                onChange={(e) => {
                  setSelectedUserId(e.target.value);
                  const selected = driverMembers.find((m) => m.userId === e.target.value);
                  if (selected) setFullName(selected.fullName);
                }}
              >
                <option value="">Select a driver member...</option>
                {driverMembers.map((m) => (
                  <option key={m.userId} value={m.userId}>
                    {m.fullName} ({m.email})
                  </option>
                ))}
              </select>
            )}
          </FormField>

          <FormField label="Full Legal Name" required htmlFor="driverName">
            <Input
              id="driverName"
              placeholder="e.g. Tarek Mansour"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
            />
          </FormField>

          <FormField label="Commercial License Number" required htmlFor="licenseNum">
            <Input
              id="licenseNum"
              placeholder="e.g. DL-9812-EGY"
              value={licenseNumber}
              onChange={(e) => setLicenseNumber(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setRegisterModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleRegister}
              disabled={!selectedUserId}
              isLoading={registerMutation.isPending}
            >
              Register Profile
            </Button>
          </div>
        </div>
      </Modal>

      {/* Edit Driver Modal */}
      <Modal
        isOpen={!!editModalDriver}
        onClose={() => setEditModalDriver(null)}
        title="Edit Driver Profile"
        description="Update driver full name or commercial license details."
      >
        <div className="space-y-4">
          <FormField label="Full Name" required htmlFor="editName">
            <Input
              id="editName"
              value={editFullName}
              onChange={(e) => setEditFullName(e.target.value)}
            />
          </FormField>

          <FormField label="License Number" required htmlFor="editLic">
            <Input
              id="editLic"
              value={editLicense}
              onChange={(e) => setEditLicense(e.target.value)}
            />
          </FormField>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setEditModalDriver(null)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleUpdate}
              isLoading={updateMutation.isPending}
            >
              Save Changes
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
