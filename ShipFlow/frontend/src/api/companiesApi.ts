import { apiClient } from './client';
import { AddMemberCommand, CompanyDto, CreateCompanyCommand, GetMembersQuery, MemberDto } from './types';

export const companiesApi = {
  create: async (command: CreateCompanyCommand, signal?: AbortSignal): Promise<CompanyDto> => {
    const res = await apiClient.post<CompanyDto>('/api/companies', command, { signal });
    return res.data;
  },

  getMine: async (signal?: AbortSignal): Promise<CompanyDto> => {
    const res = await apiClient.get<CompanyDto>('/api/companies/me', { signal });
    return res.data;
  },

  getMembers: async (query?: GetMembersQuery, signal?: AbortSignal): Promise<MemberDto[]> => {
    const params = new URLSearchParams();
    if (query?.includeFormer != null) {
      params.append('includeFormer', query.includeFormer.toString());
    }
    const res = await apiClient.get<MemberDto[]>('/api/companies/me/members', { params, signal });
    return res.data;
  },

  addMember: async (command: AddMemberCommand, signal?: AbortSignal): Promise<MemberDto> => {
    const res = await apiClient.post<MemberDto>('/api/companies/me/members', command, { signal });
    return res.data;
  },

  removeMember: async (userId: string, signal?: AbortSignal): Promise<void> => {
    await apiClient.delete(`/api/companies/me/members/${userId}`, { signal });
  },
};
