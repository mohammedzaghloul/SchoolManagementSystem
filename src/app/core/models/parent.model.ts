export interface ParentChildSummary {
  id: number;
  fullName: string;
  email?: string;
}

export interface Parent {
  id: number;
  userId?: string;
  fullName: string;
  email: string;
  phone?: string;
  address?: string;
  childrenCount?: number;
  children?: ParentChildSummary[];
}

export interface CreateParentRequest {
  fullName: string;
  email: string;
  phone?: string;
  address?: string;
  password: string;
}
