import { UsersView } from "@/components/users/UsersView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Role, User } from "@/lib/types";

export default async function UsersPage() {
  const session = await requireScreen("users");

  const users = await fetchFromApi<User[]>("users");

  // Listing the roles requires the roles screen. Someone holding only "users" still has to
  // assign a role, so a missing permission must not take the page down.
  const roles = await fetchFromApi<Role[]>("roles").catch(() => [] as Role[]);

  return <UsersView initialUsers={users} roles={roles} currentUserCode={session.user.code} />;
}
