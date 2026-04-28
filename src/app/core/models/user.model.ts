
// core/models/user.model.ts
export interface User {
  id?: string | number;
  fullName?: string;
  email: string;
  phone?: string;
  avatar?: string;
  role: string;
  createdAt?: Date;
  lastLogin?: Date;
  sessionCount?: number;
  address?: string;
  permissions?: string[];
  isLinkedToCentralAuth?: boolean;
}


