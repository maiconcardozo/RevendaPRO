import { UsersView } from "@/components/users/UsersView";
import { fetchFromApi } from "@/lib/server";
import { requireScreen } from "@/lib/session";
import type { Role, User, Yard } from "@/lib/types";

export default async function UsersPage() {
  const session = await requireScreen("users");

  const users = await fetchFromApi<User[]>("users");

  // Listing the roles requires the roles screen. Someone holding only "users" still has to
  // assign a role, so a missing permission must not take the page down.
  const roles = await fetchFromApi<Role[]>("roles").catch(() => [] as Role[]);

  // Os pátios servem para prender um parceiro ao lugar dele (M24). Mesma razão dos perfis:
  // uma permissão que falte jamais pode derrubar a página inteira.
  const yards = await fetchFromApi<Yard[]>("yards").catch(() => [] as Yard[]);

  return (
    <UsersView
      initialUsers={users}
      roles={roles}
      yards={yards}
      currentUserCode={session.user.code}
    />
  );
}
