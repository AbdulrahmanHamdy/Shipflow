export type CompanyRole = 'Admin' | 'Dispatcher' | 'Driver';

export interface CompanyDto {
  id: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
  myRole: CompanyRole | string;
}

export interface MemberDto {
  userId: string;
  email: string;
  fullName: string;
  role: CompanyRole | string;
  isActive: boolean;
  joinedAtUtc: string;
  leftAtUtc: string | null;
}

export interface CreateCompanyCommand {
  name: string;
}

export interface AddMemberCommand {
  email: string;
  role: 'Dispatcher' | 'Driver';
}

export interface GetMembersQuery {
  includeFormer?: boolean;
}
