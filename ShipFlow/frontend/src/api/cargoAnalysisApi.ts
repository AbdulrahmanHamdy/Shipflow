import { apiClient } from './client';
import {
  CargoAnalysisResponseDto,
  UpdateCargoItemInputDto,
  VehicleRecommendationResultDto,
} from './types/cargoAnalysis';

export const cargoAnalysisApi = {
  analyze: async (images: File[]): Promise<CargoAnalysisResponseDto> => {
    const formData = new FormData();
    images.forEach((img) => {
      formData.append('images', img);
    });

    const response = await apiClient.post<CargoAnalysisResponseDto>(
      '/api/cargo-analysis/analyze',
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
        timeout: 90000, // 90s for Gemini Vision multi-image inference
      }
    );
    return response.data;
  },

  getById: async (id: string): Promise<CargoAnalysisResponseDto> => {
    const response = await apiClient.get<CargoAnalysisResponseDto>(
      `/api/cargo-analysis/${id}`
    );
    return response.data;
  },

  updateItems: async (
    id: string,
    items: UpdateCargoItemInputDto[]
  ): Promise<CargoAnalysisResponseDto> => {
    const response = await apiClient.put<CargoAnalysisResponseDto>(
      `/api/cargo-analysis/${id}/items`,
      { items }
    );
    return response.data;
  },

  confirm: async (id: string): Promise<CargoAnalysisResponseDto> => {
    const response = await apiClient.post<CargoAnalysisResponseDto>(
      `/api/cargo-analysis/${id}/confirm`
    );
    return response.data;
  },

  recommendVehicles: async (
    items: UpdateCargoItemInputDto[]
  ): Promise<VehicleRecommendationResultDto> => {
    const response = await apiClient.post<VehicleRecommendationResultDto>(
      '/api/cargo-analysis/recommend-vehicles',
      { items }
    );
    return response.data;
  },
};
