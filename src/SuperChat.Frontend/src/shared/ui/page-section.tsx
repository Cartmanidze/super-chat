import type { PropsWithChildren, ReactNode } from "react";

type PageSectionProps = PropsWithChildren<{
  eyebrow: string;
  title: string;
  description: string;
  aside?: ReactNode;
}>;

export function PageSection({ eyebrow, title, description, aside, children }: PageSectionProps) {
  return (
    <section className="page-section">
      <header className="page-header">
        <div>
          <div className="eyebrow">{eyebrow}</div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        {aside ? <div className="page-header-aside">{aside}</div> : null}
      </header>
      {children}
    </section>
  );
}
