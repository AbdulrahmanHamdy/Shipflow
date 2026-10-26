import React, { useState } from 'react';
import {
  useMyCompanyQuery,
  useCompanyMembersQuery,
  useAddCompanyMemberMutation,
  useRemoveCompanyMemberMutation,
} from '@/features/carrier/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { useAuth } from '@/features/auth/AuthContext';
import { MemberDto, AppError } from '@/api/types';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/Table';
import { Modal } from '@/components/ui/Modal';
import { FormField } from '@/components/ui/FormField';
import { Input } from '@/components/ui/Input';
import { Skeleton } from '@/components/ui/Skeleton';
import { EmptyState } from '@/components/ui/EmptyState';
import { ErrorState } from '@/components/ui/ErrorState';
import { useToast } from '@/components/ui/Toast';
import { formatDate, formatDateTime } from '@/lib/formatters';
import { Building2, Users, UserPlus, Trash2, ShieldCheck, Mail, AlertTriangle } from 'lucide-react';

export const CarrierTeamPage: React.FC = () => {
  usePageTitle('Company & Team');
  const toast = useToast();
  const { hasRole } = useAuth();
  const isCompanyAdmin = hasRole('CompanyAdmin');

  const { data: company, isLoading: isCompanyLoading } = useMyCompanyQuery();
  const { data: members = [], isLoading: isMembersLoading, isError, error, refetch } = useCompanyMembersQuery();

  const addMemberMutation = useAddCompanyMemberMutation();
  const removeMemberMutation = useRemoveCompanyMemberMutation();

  const [addModalOpen, setAddModalOpen] = useState(false);
  const [memberEmail, setMemberEmail] = useState('');
  const [memberRole, setMemberRole] = useState<'Dispatcher' | 'Driver'>('Driver');

  const handleAddMember = async () => {
    if (!memberEmail.trim()) {
      toast.error('Email required', 'Please provide a valid registered user email address.');
      return;
    }

    try {
      await addMemberMutation.mutateAsync({
        email: memberEmail.trim(),
        role: memberRole,
      });

      toast.success('Member Added', `${memberEmail} has been added as a ${memberRole}.`);
      setAddModalOpen(false);
      setMemberEmail('');
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Failed to add member', appErr.message);
    }
  };

  const handleRemoveMember = async (member: MemberDto) => {
    if (!window.confirm(`Are you sure you want to remove ${member.fullName} (${member.email}) from the company? This will revoke their role and deactivate any linked driver profile.`)) {
      return;
    }

    try {
      await removeMemberMutation.mutateAsync(member.userId);
      toast.info('Member Removed', `${member.fullName} has been removed from company membership.`);
      refetch();
    } catch (err: unknown) {
      const appErr = err as AppError;
      toast.error('Removal failed', appErr.message);
    }
  };

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <PageHeader
        title="Company &amp; Team Governance"
        description="Manage company organizational details, staff authorizations, and driver membership seats."
        breadcrumbs={[{ label: 'Control Tower', href: '/carrier' }, { label: 'Company & Team' }]}
        actions={
          isCompanyAdmin ? (
            <Button
              variant="primary"
              size="sm"
              onClick={() => setAddModalOpen(true)}
              leftIcon={<UserPlus className="w-4 h-4" />}
            >
              Add Team Member
            </Button>
          ) : undefined
        }
      />

      {/* Company Identity Banner */}
      {isCompanyLoading ? (
        <Skeleton className="h-28 w-full rounded-2xl" />
      ) : company ? (
        <Card className="bg-forest-night text-white border-forest-border shadow-panel">
          <CardContent className="p-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div className="space-y-1">
              <span className="text-[10px] uppercase tracking-wider text-emerald-400 font-bold">
                Commercial Carrier Entity
              </span>
              <h2 className="text-2xl font-black text-white">{company.name}</h2>
              <p className="text-xs text-neutral-300">
                Registered on {formatDate(company.createdAtUtc)} &bull; Company ID: {company.id}
              </p>
            </div>
            <div className="flex items-center gap-3">
              <div className="text-right">
                <span className="text-[11px] text-neutral-400 block">Your Active Role</span>
                <span className="text-sm font-bold text-emerald-400">{company.myRole}</span>
              </div>
              <div className="w-12 h-12 rounded-xl bg-forest-deep flex items-center justify-center text-emerald-400 border border-forest-border">
                <Building2 className="w-6 h-6" />
              </div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {/* Team Members List */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Users className="w-5 h-5 text-brand" />
            <div>
              <CardTitle>Roster of Members &amp; Dispatchers</CardTitle>
              <CardDescription>Accounts granted operational access under this carrier entity</CardDescription>
            </div>
          </div>
        </CardHeader>

        <CardContent className="p-0">
          {isMembersLoading ? (
            <div className="p-6 space-y-3">
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          ) : isError ? (
            <div className="p-6">
              <ErrorState message={(error as any)?.message} onRetry={() => refetch()} />
            </div>
          ) : members.length === 0 ? (
            <div className="p-8">
              <EmptyState
                icon={Users}
                title="No Team Members Found"
                description="Add members to grant Dispatcher or Driver permissions."
                actionLabel="Add Member"
                onAction={() => setAddModalOpen(true)}
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Member Name</TableHead>
                  <TableHead>Email Address</TableHead>
                  <TableHead>Assigned Role</TableHead>
                  <TableHead>Joined Date</TableHead>
                  <TableHead>Status</TableHead>
                  {isCompanyAdmin && <TableHead className="text-right">Actions</TableHead>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {members.map((m) => (
                  <TableRow key={m.userId}>
                    <TableCell className="font-bold text-neutral-primary">
                      {m.fullName}
                    </TableCell>
                    <TableCell className="text-xs text-neutral-secondary">
                      {m.email}
                    </TableCell>
                    <TableCell>
                      <span
                        className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${
                          m.role === 'Admin'
                            ? 'bg-purple-100 text-purple-900'
                            : m.role === 'Dispatcher'
                            ? 'bg-sky-100 text-sky-900'
                            : 'bg-emerald-100 text-emerald-900'
                        }`}
                      >
                        {m.role}
                      </span>
                    </TableCell>
                    <TableCell className="text-xs text-neutral-secondary">
                      {formatDate(m.joinedAtUtc)}
                    </TableCell>
                    <TableCell>
                      <span
                        className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
                          m.isActive
                            ? 'bg-emerald-50 text-emerald-800'
                            : 'bg-neutral-hover text-neutral-secondary'
                        }`}
                      >
                        {m.isActive ? 'Active' : 'Departed'}
                      </span>
                    </TableCell>
                    {isCompanyAdmin && (
                      <TableCell className="text-right">
                        {m.role !== 'Admin' && m.isActive && (
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 text-xs text-red-600 hover:text-red-700 hover:bg-red-50"
                            onClick={() => handleRemoveMember(m)}
                            leftIcon={<Trash2 className="w-3 h-3" />}
                          >
                            Remove
                          </Button>
                        )}
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Add Member Modal */}
      <Modal
        isOpen={addModalOpen}
        onClose={() => setAddModalOpen(false)}
        title="Add Company Team Member"
        description="Invite an existing registered user to join your transport company organization."
      >
        <div className="space-y-4">
          <FormField
            label="User Email Address"
            required
            hint="Account must already be registered with ShipFlow"
            htmlFor="userEmail"
          >
            <Input
              id="userEmail"
              type="email"
              placeholder="e.g. driver1@company.com"
              value={memberEmail}
              onChange={(e) => setMemberEmail(e.target.value)}
              leftIcon={<Mail className="w-4 h-4" />}
            />
          </FormField>

          <FormField label="Assign Role" required htmlFor="roleSelect">
            <select
              id="roleSelect"
              className="w-full h-10 px-3 py-2 rounded-lg border border-neutral-border bg-white text-sm text-neutral-primary focus:border-brand focus:ring-1 focus:ring-brand"
              value={memberRole}
              onChange={(e) => setMemberRole(e.target.value as 'Dispatcher' | 'Driver')}
            >
              <option value="Driver">Driver (Executes deliveries &amp; telemetry)</option>
              <option value="Dispatcher">Dispatcher (Submits quotes &amp; schedules fleet)</option>
            </select>
          </FormField>

          <div className="p-3.5 rounded-xl bg-neutral-appBg border border-neutral-border text-xs text-neutral-secondary leading-relaxed">
            <strong>Note on Role Assignment (D-023):</strong> Adding a user grants the company membership immediately. The user must sign in again to receive the updated role in their JWT access token.
          </div>

          <div className="flex items-center justify-end gap-2.5 pt-3 border-t border-neutral-border">
            <Button variant="outline" size="sm" onClick={() => setAddModalOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleAddMember}
              isLoading={addMemberMutation.isPending}
            >
              Confirm Member
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
};
