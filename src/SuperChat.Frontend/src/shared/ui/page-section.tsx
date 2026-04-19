import type { PropsWithChildren, ReactNode } from "react";

type PageSectionProps = PropsWithChildren<{
  eyebrow?: string;
  title?: string;
  description?: string;
  aside?: ReactNode;
}>;

export function PageSection({ eyebrow, title, description, aside, children }: PageSectionProps) {
  const hasHeader = Boolean(eyebrow || title || description || aside);
  return (
    <section className="page-section">
      {hasHeader ? (
        <header className="page-header">
          <div>
            {eyebrow ? <div className="eyebrow">{eyebrow}</div> : null}
            {title ? <h2>{title}</h2> : null}
            {description ? <p>{description}</p> : null}
          </div>
          {aside ? <div className="page-header-aside">{aside}</div> : null}
        </header>
      ) : null}
      {children}
    </section>
  );
}
